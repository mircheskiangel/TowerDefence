using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense.EditorTools
{
    // Editor-only tool that builds the whole level from code: placeholder sprites,
    // materials, the scene hierarchy and all component wiring.
    // Menu: "Tower Defense/Build Level". Safe to run repeatedly: it always creates a
    // fresh scene and overwrites generated assets in place (GUIDs are kept).
    public static class LevelBuilder
    {
        // --- Paths ---
        private const string ScenePath = "Assets/Scenes/TowerDefense.unity";
        private const string GeneratedFolder = "Assets/Generated";
        private const string SpritesFolder = GeneratedFolder + "/Sprites";
        private const string MaterialsFolder = GeneratedFolder + "/Materials";
        private const string DataFolder = GeneratedFolder + "/Data";
        private const string PrefabsFolder = GeneratedFolder + "/Prefabs";
        private const string EnemyPrefabPath = PrefabsFolder + "/Enemy.prefab";
        private const string TowerPrefabPath = PrefabsFolder + "/Tower.prefab";

        // --- Level layout ---
        private const int GridWidth = 16;
        private const int GridHeight = 10;
        private const float CellSize = 1f;
        private static readonly Vector2Int SpawnCell = new Vector2Int(0, 5);
        private static readonly Vector2Int GoalCell = new Vector2Int(15, 4);

        // --- Colors ---
        private static readonly Color BackgroundColor = new Color(0.08f, 0.09f, 0.12f);
        private static readonly Color TileColorA = new Color(0.24f, 0.36f, 0.24f);
        private static readonly Color TileColorB = new Color(0.27f, 0.40f, 0.27f);
        private static readonly Color SpawnColor = new Color(0.20f, 0.55f, 1.00f);
        private static readonly Color GoalColor = new Color(0.95f, 0.30f, 0.25f);
        private static readonly Color BasicTowerColor = new Color(0.30f, 0.75f, 0.95f);
        private static readonly Color RangeColor = new Color(0.55f, 0.85f, 1.00f, 0.9f);
        private static readonly Color MessageColor = new Color(1.00f, 0.35f, 0.30f);

        // --- Sorting orders (higher draws on top) ---
        public const int OrderTiles = 0;
        public const int OrderMarkers = 1;
        public const int OrderRange = 3;
        public const int OrderTowers = 5;
        public const int OrderEnemies = 10;
        public const int OrderHighlight = 15;
        public const int OrderPathLine = 20;

        private const int SpritePixels = 32;
        private const int RingPixels = 128; // Higher resolution: the range ring is scaled up to ~5 cells.

        [MenuItem("Tower Defense/Build Level")]
        public static void BuildLevel()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!EnsureTextMeshProResources())
                return;

            EnsureFolder(GeneratedFolder);
            EnsureFolder(SpritesFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabsFolder);

            // 1. Assets
            Sprite square = CreateSprite("Square", SpriteShape.Square);
            Sprite circle = CreateSprite("Circle", SpriteShape.Circle);
            Sprite ring = CreateSprite("RangeRing", SpriteShape.Ring, RingPixels);
            Material spriteMaterial = CreateSpriteMaterial();
            EnemySO grunt = CreateEnemyData("Grunt", maxHealth: 30f, moveSpeed: 2f, goldReward: 5,
                new Color(1.00f, 0.80f, 0.20f), scale: 0.5f);
            EnemySO tank = CreateEnemyData("Tank", maxHealth: 120f, moveSpeed: 1f, goldReward: 15,
                new Color(0.65f, 0.35f, 0.90f), scale: 0.8f);
            CreateEnemyPrefab(circle, spriteMaterial);
            TowerSO basicTower = CreateTowerData("Basic Tower", cost: 25, range: 2.5f, damage: 10f, fireRate: 1.5f, BasicTowerColor);
            CreateTowerPrefab(square, spriteMaterial);

            // 2. Fresh scene (replaces any previous build)
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 3. Grid
            GameObject gridGo = new GameObject("Grid");
            GridManager grid = gridGo.AddComponent<GridManager>();
            grid.Configure(GridWidth, GridHeight, CellSize, Vector2.zero, SpawnCell, GoalCell);
            BuildTiles(grid, square, spriteMaterial);
            CreateMarker("SpawnMarker", grid, SpawnCell, SpawnColor, square, spriteMaterial);
            CreateMarker("GoalMarker", grid, GoalCell, GoalColor, square, spriteMaterial);

            // 4. Camera
            BuildCamera(grid);

            // 5. Pathfinding debug (path line, P toggles)
            BuildPathDebug(grid, spriteMaterial);

            // Prefabs are re-loaded here: opening a new scene can unload assets created before it,
            // which would leave a dead reference and an empty field in the saved scene.
            Enemy enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Tower towerPrefab = AssetDatabase.LoadAssetAtPath<Tower>(TowerPrefabPath);

            // 6. Temporary enemy spawner (replaced by WaveManager in M5).
            BuildTestSpawner(grid, enemyPrefab, grunt, tank);

            // 7. Player resources, tower placement and UI
            PlayerResources resources = BuildPlayerResources();
            MessageDisplay messages = BuildUI(resources);
            BuildTowerPlacer(grid, resources, towerPrefab, basicTower, square, ring, spriteMaterial, messages);
            BuildDebugKeys(resources); // TEMPORARY

            // 8. Save and register in Build Settings
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[LevelBuilder] Level built: " + ScenePath);
        }

        // ---------------------------------------------------------------- Scene

        private static void BuildTiles(GridManager grid, Sprite sprite, Material material)
        {
            Transform parent = new GameObject("Tiles").transform;
            parent.SetParent(grid.transform, false);

            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Color color = (x + y) % 2 == 0 ? TileColorA : TileColorB;
                    GameObject tile = CreateSpriteObject($"Tile_{x}_{y}", sprite, material, color, OrderTiles);
                    tile.transform.SetParent(parent, false);
                    tile.transform.position = grid.CellToWorld(cell);
                    tile.transform.localScale = Vector3.one * grid.CellSize;
                }
            }
        }

        private static void CreateMarker(string name, GridManager grid, Vector2Int cell, Color color, Sprite sprite, Material material)
        {
            GameObject marker = CreateSpriteObject(name, sprite, material, color, OrderMarkers);
            marker.transform.SetParent(grid.transform, false);
            marker.transform.position = grid.CellToWorld(cell);
            marker.transform.localScale = Vector3.one * grid.CellSize * 0.9f;
        }

        private static void BuildCamera(GridManager grid)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            camGo.AddComponent<AudioListener>();

            CameraFitter fitter = camGo.AddComponent<CameraFitter>();
            fitter.SetGrid(grid);
            fitter.Fit();
        }

        private static void BuildPathDebug(GridManager grid, Material material)
        {
            GameObject debugGo = new GameObject("PathDebug");
            PathDebugView view = debugGo.AddComponent<PathDebugView>();
            SetField(view, "grid", grid);
            SetField(view, "lineMaterial", material);
            SetField(view, "sortingOrder", OrderPathLine);
        }

        private static PlayerResources BuildPlayerResources()
        {
            GameObject go = new GameObject("PlayerResources");
            return go.AddComponent<PlayerResources>();
        }

        private static void BuildTowerPlacer(GridManager grid, PlayerResources resources, Tower towerPrefab,
            TowerSO towerData, Sprite square, Sprite ring, Material material, MessageDisplay messages)
        {
            GameObject go = new GameObject("TowerPlacer");
            TowerPlacer placer = go.AddComponent<TowerPlacer>();

            GameObject highlight = CreateSpriteObject("HoverHighlight", square, material, new Color(1f, 1f, 1f, 0.45f), OrderHighlight);
            highlight.transform.SetParent(go.transform, false);
            SpriteRenderer highlightRenderer = highlight.GetComponent<SpriteRenderer>();
            highlightRenderer.enabled = false;

            GameObject range = CreateSpriteObject("RangeCircle", ring, material, RangeColor, OrderRange);
            range.transform.SetParent(go.transform, false);
            SpriteRenderer rangeRenderer = range.GetComponent<SpriteRenderer>();
            rangeRenderer.enabled = false;

            SetField(placer, "grid", grid);
            SetField(placer, "resources", resources);
            SetField(placer, "cam", Camera.main);
            SetField(placer, "towerPrefab", towerPrefab);
            SetField(placer, "towerData", towerData);
            SetField(placer, "highlight", highlightRenderer);
            SetField(placer, "rangeCircle", rangeRenderer);
            SetField(placer, "messages", messages);
        }

        // TEMPORARY debug shortcuts (G = +100 gold). Remove this call and DebugKeys.cs to drop them.
        private static void BuildDebugKeys(PlayerResources resources)
        {
            GameObject go = new GameObject("DebugKeys");
            DebugKeys keys = go.AddComponent<DebugKeys>();
            SetField(keys, "resources", resources);
        }

        // Screen-space canvas with the HUD label, plus an EventSystem that uses the
        // new Input System (needed so TowerPlacer can ignore clicks on UI).
        private static MessageDisplay BuildUI(PlayerResources resources)
        {
            GameObject canvasGo = new GameObject("Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject labelGo = new GameObject("ResourcesLabel", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);
            RectTransform rect = labelGo.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -16f);
            rect.sizeDelta = new Vector2(700f, 60f);

            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Gold: 0  Lives: 0";
            label.fontSize = 36;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false; // A label should never swallow clicks meant for the grid.

            ResourcesHUD hud = labelGo.AddComponent<ResourcesHUD>();
            SetField(hud, "resources", resources);
            SetField(hud, "label", label);

            // Centered message near the top ("Path blocked!"), invisible until shown.
            GameObject messageGo = new GameObject("MessageLabel", typeof(RectTransform));
            messageGo.transform.SetParent(canvasGo.transform, false);
            RectTransform messageRect = messageGo.GetComponent<RectTransform>();
            messageRect.anchorMin = messageRect.anchorMax = new Vector2(0.5f, 1f);
            messageRect.pivot = new Vector2(0.5f, 1f);
            messageRect.anchoredPosition = new Vector2(0f, -80f);
            messageRect.sizeDelta = new Vector2(900f, 80f);

            TextMeshProUGUI messageLabel = messageGo.AddComponent<TextMeshProUGUI>();
            messageLabel.text = "";
            messageLabel.fontSize = 56;
            messageLabel.fontStyle = FontStyles.Bold;
            messageLabel.color = MessageColor;
            messageLabel.alignment = TextAlignmentOptions.Center;
            messageLabel.raycastTarget = false;
            messageLabel.alpha = 0f;

            MessageDisplay messages = messageGo.AddComponent<MessageDisplay>();
            SetField(messages, "label", messageLabel);

            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
            return messages;
        }

        private static void BuildTestSpawner(GridManager grid, Enemy enemyPrefab, EnemySO grunt, EnemySO tank)
        {
            GameObject go = new GameObject("TestSpawner");
            TestSpawner spawner = go.AddComponent<TestSpawner>();
            SetField(spawner, "grid", grid);
            SetField(spawner, "enemyPrefab", enemyPrefab);
            SetField(spawner, "gruntData", grunt);
            SetField(spawner, "tankData", tank);
        }

        // Assigns a [SerializeField] (private) field through Unity's serialization,
        // so the value is saved with the scene.
        private static void SetField(Object target, string fieldName, object value)
        {
            if (value is Object unityObj && unityObj == null)
                Debug.LogError($"[LevelBuilder] Assigning a missing object to '{fieldName}' on {target.GetType().Name}");
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError($"[LevelBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }
            switch (value)
            {
                case Object obj: prop.objectReferenceValue = obj; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case bool b: prop.boolValue = b; break;
                case Color c: prop.colorValue = c; break;
                default: Debug.LogError($"[LevelBuilder] Unsupported type for '{fieldName}'"); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateSpriteObject(string name, Sprite sprite, Material material, Color color, int order)
        {
            GameObject go = new GameObject(name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        // --------------------------------------------------------------- Assets

        private enum SpriteShape { Square, Circle, Ring }

        // Generates a white PNG (tinted later via SpriteRenderer.color) and imports it as a sprite
        // that is exactly 1 world unit wide (pixels per unit = texture size).
        // Ring = faint filled disc with a solid outline, used for the tower range display.
        private static Sprite CreateSprite(string name, SpriteShape shape, int size = SpritePixels)
        {
            string path = $"{SpritesFolder}/{name}.png";

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 0.5f;
            float ringWidth = size * 0.025f;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float alpha;
                    switch (shape)
                    {
                        case SpriteShape.Square: alpha = 1f; break;
                        case SpriteShape.Circle: alpha = d <= radius ? 1f : 0f; break;
                        default: alpha = d > radius ? 0f : (d >= radius - ringWidth ? 1f : 0.12f); break;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = size;
            importer.filterMode = shape == SpriteShape.Square ? FilterMode.Point : FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Creates the EnemySO asset, or updates it in place (same GUID) if it already exists.
        private static EnemySO CreateEnemyData(string name, float maxHealth, float moveSpeed, int goldReward, Color color, float scale)
        {
            string path = $"{DataFolder}/{name}.asset";
            EnemySO data = AssetDatabase.LoadAssetAtPath<EnemySO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<EnemySO>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.displayName = name;
            data.maxHealth = maxHealth;
            data.moveSpeed = moveSpeed;
            data.goldReward = goldReward;
            data.color = color;
            data.scale = scale;
            EditorUtility.SetDirty(data);
            return data;
        }

        // One generic enemy prefab (white circle); color/scale/stats come from EnemySO at spawn.
        private static void CreateEnemyPrefab(Sprite circle, Material material)
        {
            GameObject go = CreateSpriteObject("Enemy", circle, material, Color.white, OrderEnemies);
            Enemy enemy = go.AddComponent<Enemy>();
            SetField(enemy, "body", go.GetComponent<SpriteRenderer>());

            PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefabPath); // Overwrites, keeps GUID.
            Object.DestroyImmediate(go);
        }

        // Creates the TowerSO asset, or updates it in place (same GUID) if it already exists.
        private static TowerSO CreateTowerData(string name, int cost, float range, float damage, float fireRate, Color color)
        {
            string path = $"{DataFolder}/{name.Replace(" ", "")}.asset";
            TowerSO data = AssetDatabase.LoadAssetAtPath<TowerSO>(path);
            if (data == null)
            {
                data = ScriptableObject.CreateInstance<TowerSO>();
                AssetDatabase.CreateAsset(data, path);
            }
            data.displayName = name;
            data.cost = cost;
            data.range = range;
            data.damage = damage;
            data.fireRate = fireRate;
            data.color = color;
            EditorUtility.SetDirty(data);
            return data;
        }

        // One generic tower prefab (white square, slightly smaller than a cell); colored from TowerSO.
        private static void CreateTowerPrefab(Sprite square, Material material)
        {
            GameObject go = CreateSpriteObject("Tower", square, material, Color.white, OrderTowers);
            go.transform.localScale = Vector3.one * CellSize * 0.8f;
            Tower tower = go.AddComponent<Tower>();
            SetField(tower, "body", go.GetComponent<SpriteRenderer>());

            PrefabUtility.SaveAsPrefabAsset(go, TowerPrefabPath);
            Object.DestroyImmediate(go);
        }

        // TextMeshPro needs its "Essential Resources" (default font + settings) inside Assets/.
        // If they are missing, import them and ask the user to run the builder again,
        // because the package import finishes asynchronously.
        private static bool EnsureTextMeshProResources()
        {
            if (AssetDatabase.FindAssets("t:TMP_Settings").Length > 0) return true;
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            Debug.LogWarning("[LevelBuilder] Imported TextMesh Pro Essential Resources. " +
                             "Run 'Tower Defense/Build Level' again once the import has finished.");
            return false;
        }

        // Unlit sprite material so the level needs no 2D lights to be visible.
        private static Material CreateSpriteMaterial()
        {
            string path = $"{MaterialsFolder}/SpriteUnlit.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
