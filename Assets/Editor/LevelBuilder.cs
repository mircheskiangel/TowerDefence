using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        // --- Sorting orders (higher draws on top) ---
        public const int OrderTiles = 0;
        public const int OrderMarkers = 1;
        public const int OrderObstacles = 2;
        public const int OrderEnemies = 10;
        public const int OrderPathLine = 20;

        private const int SpritePixels = 32;

        [MenuItem("Tower Defense/Build Level")]
        public static void BuildLevel()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            EnsureFolder(GeneratedFolder);
            EnsureFolder(SpritesFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(PrefabsFolder);

            // 1. Assets
            Sprite square = CreateSprite("Square", SpriteShape.Square);
            Sprite circle = CreateSprite("Circle", SpriteShape.Circle);
            Material spriteMaterial = CreateSpriteMaterial();
            EnemySO grunt = CreateEnemyData("Grunt", maxHealth: 30f, moveSpeed: 2f, goldReward: 5,
                new Color(1.00f, 0.80f, 0.20f), scale: 0.5f);
            EnemySO tank = CreateEnemyData("Tank", maxHealth: 120f, moveSpeed: 1f, goldReward: 15,
                new Color(0.65f, 0.35f, 0.90f), scale: 0.8f);
            Enemy enemyPrefab = CreateEnemyPrefab(circle, spriteMaterial);

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

            // 5. Pathfinding debug (path line + temporary test walls)
            BuildPathDebug(grid, square, spriteMaterial);

            // 6. Temporary enemy spawner (replaced by WaveManager in M5)
            BuildTestSpawner(grid, enemyPrefab, grunt, tank);

            // 7. Save and register in Build Settings
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

        private static void BuildPathDebug(GridManager grid, Sprite square, Material material)
        {
            GameObject debugGo = new GameObject("PathDebug");
            PathDebugView view = debugGo.AddComponent<PathDebugView>();
            SetField(view, "grid", grid);
            SetField(view, "lineMaterial", material);
            SetField(view, "sortingOrder", OrderPathLine);

            // TEMPORARY: remove once towers can block cells.
            GameObject testGo = new GameObject("PathTestObstacles");
            PathTestObstacles test = testGo.AddComponent<PathTestObstacles>();
            SetField(test, "grid", grid);
            SetField(test, "blockSprite", square);
            SetField(test, "blockMaterial", material);
            SetField(test, "sortingOrder", OrderObstacles);
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

        private enum SpriteShape { Square, Circle }

        // Generates a white PNG (tinted later via SpriteRenderer.color) and imports it as a sprite
        // where SpritePixels pixels = 1 world unit.
        private static Sprite CreateSprite(string name, SpriteShape shape)
        {
            string path = $"{SpritesFolder}/{name}.png";

            Texture2D tex = new Texture2D(SpritePixels, SpritePixels, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2((SpritePixels - 1) * 0.5f, (SpritePixels - 1) * 0.5f);
            float radius = SpritePixels * 0.5f;
            for (int x = 0; x < SpritePixels; x++)
            {
                for (int y = 0; y < SpritePixels; y++)
                {
                    bool filled = shape == SpriteShape.Square || Vector2.Distance(new Vector2(x, y), center) <= radius - 0.5f;
                    tex.SetPixel(x, y, filled ? Color.white : Color.clear);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = SpritePixels;
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
        private static Enemy CreateEnemyPrefab(Sprite circle, Material material)
        {
            string path = $"{PrefabsFolder}/Enemy.prefab";
            GameObject go = CreateSpriteObject("Enemy", circle, material, Color.white, OrderEnemies);
            Enemy enemy = go.AddComponent<Enemy>();
            SetField(enemy, "body", go.GetComponent<SpriteRenderer>());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path); // Overwrites, keeps GUID.
            Object.DestroyImmediate(go);
            return prefab.GetComponent<Enemy>();
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
