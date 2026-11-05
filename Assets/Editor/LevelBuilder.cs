using System.Collections.Generic;
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
        private const string WavesFolder = DataFolder + "/Waves";
        private const string PrefabsFolder = GeneratedFolder + "/Prefabs";
        private const string EnemyPrefabPath = PrefabsFolder + "/Enemy.prefab";
        private const string TowerPrefabPath = PrefabsFolder + "/Tower.prefab";
        private const string ProjectilePrefabPath = PrefabsFolder + "/Projectile.prefab";
        // Isometric skin: Assets/Art/DefaultSkin.asset, filled from the PNGs in Assets/Art/Generated/.
        private const string ArtFolder = "Assets/Art";
        private const string SkinPath = ArtFolder + "/DefaultSkin.asset";

        // --- Level layout ---
        private const int GridWidth = 16;
        private const int GridHeight = 10;
        private const float CellSize = 1f;
        private static readonly Vector2Int SpawnCell = new Vector2Int(0, 5);
        private static readonly Vector2Int GoalCell = new Vector2Int(15, 4);
        // Isometric island: one ring of decorative (non-playable) cells around the board.
        private const int RingWidth = 1;
        private static readonly Color RingTint = new Color(0.88f, 0.88f, 0.88f);
        private const int DecorSeed = 11;
        // How the board is drawn when the level starts (V switches at runtime).
        private const GridProjectionType DefaultStartProjection = GridProjectionType.Isometric;

        // --- Balance (tuned in M6 with Assets/Editor/BalanceBot.cs) ---
        private const int StartGold = 100;
        private const int StartLives = 10;

        // --- Colors ---
        private static readonly Color BackgroundColor = new Color(0.08f, 0.09f, 0.12f);
        private static readonly Color TileColorA = new Color(0.24f, 0.36f, 0.24f);
        private static readonly Color TileColorB = new Color(0.27f, 0.40f, 0.27f);
        private static readonly Color SpawnColor = new Color(0.20f, 0.55f, 1.00f);
        private static readonly Color GoalColor = new Color(0.95f, 0.30f, 0.25f);
        private static readonly Color BasicTowerColor = new Color(0.30f, 0.75f, 0.95f);
        private static readonly Color ProjectileColor = new Color(1.00f, 0.92f, 0.25f);
        private static readonly Color RangeColor = new Color(0.55f, 0.85f, 1.00f, 0.9f);
        private static readonly Color MessageColor = new Color(1.00f, 0.35f, 0.30f);

        // --- Sorting orders (higher draws on top) ---
        public const int OrderTiles = 0;
        public const int OrderMarkers = 1;
        public const int OrderRoad = 1;      // Isometric dirt road over the grass tiles.
        public const int OrderCliffs = -2;   // Isometric cliff faces, under the island tiles (-1).
        public const int OrderBackdrop = -100;
        public const int OrderRange = 3;
        public const int OrderTowers = 5;
        public const int OrderEnemies = 10;
        public const int OrderProjectiles = 12;
        public const int OrderHighlight = 15;
        public const int OrderHealthBarBack = 16;
        public const int OrderHealthBarFill = 17;
        public const int OrderPathLine = 20;

        private const int SpritePixels = 32;
        private const int RingPixels = 128; // Higher resolution: the range ring is scaled up to ~5 cells.
        private const int DiamondPixelsWide = 64; // Diamond texture is 64 x 32 pixels = 1 x 0.5 world units.

        [MenuItem("Tower Defense/Build Level")]
        public static void BuildLevel()
        {
            BuildLevel(DefaultStartProjection);
        }

        // Same level, but starting in the top-down view (useful to compare the two views).
        [MenuItem("Tower Defense/Build Level (Top-Down Start)")]
        public static void BuildLevelTopDown()
        {
            BuildLevel(GridProjectionType.TopDown);
        }

        public static void BuildLevel(GridProjectionType startProjection)
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!EnsureTextMeshProResources())
                return;

            EnsureFolder(GeneratedFolder);
            EnsureFolder(SpritesFolder);
            EnsureFolder(MaterialsFolder);
            EnsureFolder(DataFolder);
            EnsureFolder(WavesFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ArtFolder);

            // 1. Assets
            SkinSet skin = CreateSkin();
            Sprite square = CreateSprite("Square", SpriteShape.Square);
            Sprite circle = CreateSprite("Circle", SpriteShape.Circle);
            Sprite ring = CreateSprite("RangeRing", SpriteShape.Ring, RingPixels);
            Sprite diamond = CreateDiamondSprite("Diamond", DiamondPixelsWide);
            Sprite towerIso = CreateStandingTowerSprite("TowerIso");
            Sprite towerBase = CreateTowerBaseSprite("TowerIsoBase");
            Sprite diamondOutline = CreateDiamondOutlineSprite("DiamondOutline", DiamondPixelsWide);
            Sprite enemyIso = CreateStandingCircleSprite("EnemyIso");
            Sprite shadow = CreateShadowSprite("Shadow");
            Material spriteMaterial = CreateSpriteMaterial();
            EnemySO grunt = CreateEnemyData("Grunt", maxHealth: 76f, moveSpeed: 2.3f, goldReward: 4,
                new Color(1.00f, 0.80f, 0.20f), scale: 0.5f, SkinSlot.Grunt, SkinSlot.GruntShadow, IsoArt.GruntBarPixels);
            EnemySO tank = CreateEnemyData("Tank", maxHealth: 400f, moveSpeed: 1.1f, goldReward: 10,
                new Color(0.65f, 0.35f, 0.90f), scale: 0.8f, SkinSlot.Tank, SkinSlot.TankShadow, IsoArt.TankBarPixels);
            CreateEnemyPrefab(circle, square, enemyIso, shadow, spriteMaterial, skin);
            List<WaveSO> waves = CreateWaves(grunt, tank);
            TowerSO basicTower = CreateTowerData("Basic Tower", cost: 25, range: 2.5f, damage: 10f, fireRate: 1.5f,
                projectileSpeed: 8f, BasicTowerColor);
            CreateProjectilePrefab(circle, spriteMaterial, skin); // Before the tower prefab, which references it.
            CreateTowerPrefab(square, towerIso, towerBase, shadow, spriteMaterial, skin);

            // 2. Fresh scene (replaces any previous build)
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            skin = AssetDatabase.LoadAssetAtPath<SkinSet>(SkinPath); // Re-loaded, like the prefabs below.

            // 3. Grid
            GameObject gridGo = new GameObject("Grid");
            GridManager grid = gridGo.AddComponent<GridManager>();
            grid.Configure(GridWidth, GridHeight, CellSize, Vector2.zero, SpawnCell, GoalCell, startProjection);
            BuildBoard(grid, square, diamond, spriteMaterial, skin);

            // 4. Camera
            BuildCamera(grid);

            // 5. Pathfinding debug (path line, P toggles)
            PathDebugView pathView = BuildPathDebug(grid, spriteMaterial);
            BuildRoad(grid, pathView, skin, spriteMaterial);

            // Prefabs are re-loaded here: opening a new scene can unload assets created before it,
            // which would leave a dead reference and an empty field in the saved scene.
            Enemy enemyPrefab = AssetDatabase.LoadAssetAtPath<Enemy>(EnemyPrefabPath);
            Tower towerPrefab = AssetDatabase.LoadAssetAtPath<Tower>(TowerPrefabPath);

            // 6. Player resources, lives, waves, tower placement and UI
            PlayerResources resources = BuildPlayerResources();
            WaveManager waveManager = BuildWaveManager(grid, resources, enemyPrefab, waves);
            GameManager gameManager = BuildGameManager(resources, waveManager);
            SetField(waveManager, "gameManager", gameManager);
            MessageDisplay messages = BuildUI(resources, waveManager, gameManager, grid, skin);
            BuildTowerPlacer(grid, resources, towerPrefab, basicTower, square, diamond, diamondOutline, ring, spriteMaterial, messages, gameManager, skin);
            new GameObject("TargetingModeSwitcher").AddComponent<TargetingModeSwitcher>();
            ProjectionSwitcher switcher = new GameObject("ProjectionSwitcher").AddComponent<ProjectionSwitcher>();
            SetField(switcher, "grid", grid);

            // 8. Save and register in Build Settings
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[LevelBuilder] Level built: " + ScenePath);
        }

        // ---------------------------------------------------------------- Scene

        // The floor and spawn/goal markers are drawn by BoardView (square tiles in top-down,
        // diamond tiles in isometric). It is configured here and built once in edit mode;
        // at runtime it rebuilds itself when the projection is switched.
        private static void BuildBoard(GridManager grid, Sprite square, Sprite diamond, Material material, SkinSet skin)
        {
            BoardView board = grid.gameObject.AddComponent<BoardView>();
            SetField(board, "grid", grid);
            SetField(board, "skin", skin);
            SetField(board, "squareSprite", square);
            SetField(board, "diamondSprite", diamond);
            SetField(board, "material", material);
            SetField(board, "tileColorA", TileColorA);
            SetField(board, "tileColorB", TileColorB);
            SetField(board, "spawnColor", SpawnColor);
            SetField(board, "goalColor", GoalColor);
            SetField(board, "tileSortingOrder", OrderTiles);
            SetField(board, "markerSortingOrder", OrderMarkers);
            SetField(board, "ringWidth", RingWidth);
            SetField(board, "ringTint", RingTint);
            SetField(board, "cliffSortingOrder", OrderCliffs);
            SetField(board, "backgroundSortingOrder", OrderBackdrop);
            SetDecorations(board, PlaceDecorations(grid));
            board.Rebuild();
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
            SetField(fitter, "isometricRing", (float)RingWidth);
            SetField(fitter, "isometricCliff", IsoArt.CliffWorldHeight);
            fitter.Fit();
        }

        private static PathDebugView BuildPathDebug(GridManager grid, Material material)
        {
            GameObject debugGo = new GameObject("PathDebug");
            PathDebugView view = debugGo.AddComponent<PathDebugView>();
            SetField(view, "grid", grid);
            SetField(view, "lineMaterial", material);
            SetField(view, "sortingOrder", OrderPathLine);
            return view;
        }

        // Isometric dirt road on the current path (same object as the path line, so it reads the
        // same CurrentPath). The ring cells in front of the portal and gate are always road.
        private static void BuildRoad(GridManager grid, PathDebugView pathView, SkinSet skin, Material material)
        {
            RoadView road = pathView.gameObject.AddComponent<RoadView>();
            SetField(road, "grid", grid);
            SetField(road, "pathView", pathView);
            SetField(road, "skin", skin);
            SetField(road, "material", material);
            SetField(road, "sortingOrder", OrderRoad);
            SerializedObject so = new SerializedObject(road);
            SerializedProperty cells = so.FindProperty("extraCells");
            cells.arraySize = 2;
            cells.GetArrayElementAtIndex(0).vector2IntValue = BoardView.OutsideNeighbour(grid, grid.SpawnCell);
            cells.GetArrayElementAtIndex(1).vector2IntValue = BoardView.OutsideNeighbour(grid, grid.GoalCell);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Trees, rocks and bushes on the decorative ring, placed with a fixed seed. Never on the
        // portal/gate cells or the ring cells next to them; the two front edges (lower-left
        // column x = -1 and lower-right row y = -1) get only low rocks and bushes, so nothing
        // hides the front row of the board.
        private static List<BoardView.Decoration> PlaceDecorations(GridManager grid)
        {
            var list = new List<BoardView.Decoration>();
            System.Random rng = PixelArt.Rng(DecorSeed);
            Vector2Int portal = BoardView.OutsideNeighbour(grid, grid.SpawnCell);
            Vector2Int gate = BoardView.OutsideNeighbour(grid, grid.GoalCell);
            for (int x = -RingWidth; x < grid.Width + RingWidth; x++)
            {
                for (int y = -RingWidth; y < grid.Height + RingWidth; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (grid.IsInBounds(cell)) continue;
                    double r = rng.NextDouble();
                    Vector2 offset = new Vector2((float)(rng.NextDouble() - 0.5) * 0.4f, (float)(rng.NextDouble() - 0.5) * 0.4f);
                    if (Chebyshev(cell, portal) <= 1 || Chebyshev(cell, gate) <= 1) continue;
                    bool front = x < 0 || y < 0;
                    SkinSlot slot;
                    if (!front && r < 0.50) slot = SkinSlot.Tree;
                    else if (r < 0.66) slot = SkinSlot.Rock;
                    else if (r < 0.80) slot = SkinSlot.Bush;
                    else continue;
                    int variants = slot == SkinSlot.Tree ? IsoArt.TreeVariants : slot == SkinSlot.Rock ? IsoArt.RockVariants : IsoArt.BushVariants;
                    list.Add(new BoardView.Decoration { slot = slot, variant = rng.Next(variants), gridPosition = cell + offset });
                }
            }
            return list;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        private static void SetDecorations(BoardView board, List<BoardView.Decoration> list)
        {
            SerializedObject so = new SerializedObject(board);
            SerializedProperty prop = so.FindProperty("decorations");
            prop.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                SerializedProperty e = prop.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("slot").intValue = (int)list[i].slot;
                e.FindPropertyRelative("variant").intValue = list[i].variant;
                e.FindPropertyRelative("gridPosition").vector2Value = list[i].gridPosition;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // PlayerResources plus the LivesHandler that takes a life per enemy reaching the goal.
        private static PlayerResources BuildPlayerResources()
        {
            GameObject go = new GameObject("PlayerResources");
            PlayerResources resources = go.AddComponent<PlayerResources>();
            SetField(resources, "startGold", StartGold);
            SetField(resources, "startLives", StartLives);
            LivesHandler lives = go.AddComponent<LivesHandler>();
            SetField(lives, "resources", resources);
            return resources;
        }

        private static void BuildTowerPlacer(GridManager grid, PlayerResources resources, Tower towerPrefab,
            TowerSO towerData, Sprite square, Sprite diamond, Sprite diamondOutline, Sprite ring, Material material, MessageDisplay messages, GameManager gameManager,
            SkinSet skin)
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
            SetField(placer, "squareHighlight", square);
            SetField(placer, "diamondHighlight", diamond);

            GameObject outline = CreateSpriteObject("HoverOutline", diamondOutline, material, Color.white, DepthSorter.OverlayOrder);
            outline.transform.SetParent(go.transform, false);
            outline.GetComponent<SpriteRenderer>().enabled = false;
            SetField(placer, "hoverOutline", outline.GetComponent<SpriteRenderer>());
            SetField(placer, "rangeCircle", rangeRenderer);
            RangeIndicator indicator = range.AddComponent<RangeIndicator>();
            SetField(indicator, "ringSprite", rangeRenderer);
            SetField(indicator, "material", material);
            SetField(indicator, "sortingOrder", OrderRange);
            // Isometric ellipse style (top-down keeps the ring sprite): soft white fill, thin brighter outline.
            SetField(indicator, "fillColor", new Color(1f, 1f, 1f, 0.13f));
            SetField(indicator, "lineColor", new Color(1f, 1f, 1f, 0.6f));
            SetField(indicator, "lineWidth", 0.022f);

            // Isometric skin only: semi-transparent preview of the tower on a valid hovered cell.
            GameObject preview = CreateSpriteObject("PreviewTower", square, material, Color.white, OrderTowers);
            preview.transform.SetParent(go.transform, false);
            preview.GetComponent<SpriteRenderer>().enabled = false;
            SetField(placer, "previewTower", preview.GetComponent<SpriteRenderer>());
            SetField(placer, "skin", skin);
            SetField(placer, "rangeIndicator", indicator);
            SetField(placer, "messages", messages);
            SetField(placer, "gameManager", gameManager);
        }

        // Game state machine (Playing / Won / Lost), listening to lives and waves.
        private static GameManager BuildGameManager(PlayerResources resources, WaveManager waveManager)
        {
            GameObject go = new GameObject("GameManager");
            GameManager manager = go.AddComponent<GameManager>();
            SetField(manager, "resources", resources);
            SetField(manager, "waves", waveManager);
            return manager;
        }

        // Full-screen Win/Lose overlay: dark panel, big title, result line and a Restart button.
        // Created last in the Canvas so it draws on top of the HUD. The GameOverPanel script sits
        // on the always-active parent; only the "Panel" child is shown/hidden.
        private static void BuildGameOverPanel(Transform canvas, GameManager gameManager, PlayerResources resources, WaveManager waveManager,
            GridManager grid, SkinSet skin)
        {
            GameObject root = new GameObject("GameOverUI", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            StretchFull(root.GetComponent<RectTransform>());

            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            StretchFull(panel.GetComponent<RectTransform>());
            Image background = panel.GetComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.75f);
            background.raycastTarget = true; // Blocks clicks from reaching the grid behind it.
            AddSkinnedImage(panel, background, SkinSlot.HudPanel, skin, grid);

            TextMeshProUGUI title = CreateCenteredText("Title", panel.transform, "GAME OVER", 110, new Vector2(0f, 110f), new Vector2(1200f, 150f));
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI detail = CreateCenteredText("Detail", panel.transform, "", 44, new Vector2(0f, 10f), new Vector2(1200f, 70f));

            GameObject buttonGo = new GameObject("RestartButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(panel.transform, false);
            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -110f);
            buttonRect.sizeDelta = new Vector2(360f, 96f);
            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.30f, 0.75f, 0.95f);
            AddSkinnedImage(buttonGo, buttonImage, SkinSlot.HudButton, skin, grid);
            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            TextMeshProUGUI buttonText = CreateCenteredText("Text", buttonGo.transform, "Restart (R)", 42, Vector2.zero, Vector2.zero);
            StretchFull(buttonText.rectTransform);
            buttonText.color = new Color(0.08f, 0.09f, 0.12f);
            buttonText.raycastTarget = false;

            panel.SetActive(false);

            GameOverPanel script = root.AddComponent<GameOverPanel>();
            SetField(script, "gameManager", gameManager);
            SetField(script, "resources", resources);
            SetField(script, "waves", waveManager);
            SetField(script, "panel", panel);
            SetField(script, "title", title);
            SetField(script, "detail", detail);
            SetField(script, "restartButton", button);
        }

        private static TextMeshProUGUI CreateCenteredText(string name, Transform parent, string text, float size, Vector2 position, Vector2 boxSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = boxSize;
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Screen-space canvas with the HUD label, plus an EventSystem that uses the
        // new Input System (needed so TowerPlacer can ignore clicks on UI).
        private static MessageDisplay BuildUI(PlayerResources resources, WaveManager waveManager, GameManager gameManager,
            GridManager grid, SkinSet skin)
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
            rect.anchoredPosition = new Vector2(24f, -12f);
            rect.sizeDelta = new Vector2(560f, 50f);

            TextMeshProUGUI label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "Gold: 0  Lives: 0";
            label.fontSize = 36;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false; // A label should never swallow clicks meant for the grid.

            ResourcesHUD hud = labelGo.AddComponent<ResourcesHUD>();
            SetField(hud, "resources", resources);
            SetField(hud, "label", label);

            // Second label under it: current targeting mode.
            GameObject targetingGo = new GameObject("TargetingLabel", typeof(RectTransform));
            targetingGo.transform.SetParent(canvasGo.transform, false);
            RectTransform targetingRect = targetingGo.GetComponent<RectTransform>();
            // Top-right corner (the HUD is one row along the top edge, above the board).
            targetingRect.anchorMin = targetingRect.anchorMax = new Vector2(1f, 1f);
            targetingRect.pivot = new Vector2(1f, 1f);
            targetingRect.anchoredPosition = new Vector2(-24f, -16f);
            targetingRect.sizeDelta = new Vector2(560f, 50f);

            TextMeshProUGUI targetingLabel = targetingGo.AddComponent<TextMeshProUGUI>();
            targetingLabel.text = "Targeting: First (T to change)";
            targetingLabel.fontSize = 30;
            targetingLabel.color = new Color(0.85f, 0.9f, 1f);
            targetingLabel.alignment = TextAlignmentOptions.TopRight;
            targetingLabel.raycastTarget = false;

            TargetingHUD targetingHud = targetingGo.AddComponent<TargetingHUD>();
            SetField(targetingHud, "label", targetingLabel);

            // Third label: wave status / countdown.
            GameObject waveGo = new GameObject("WaveLabel", typeof(RectTransform));
            waveGo.transform.SetParent(canvasGo.transform, false);
            RectTransform waveRect = waveGo.GetComponent<RectTransform>();
            // Top center.
            waveRect.anchorMin = waveRect.anchorMax = new Vector2(0.5f, 1f);
            waveRect.pivot = new Vector2(0.5f, 1f);
            waveRect.anchoredPosition = new Vector2(0f, -16f);
            waveRect.sizeDelta = new Vector2(720f, 50f);

            TextMeshProUGUI waveLabel = waveGo.AddComponent<TextMeshProUGUI>();
            waveLabel.text = "";
            waveLabel.fontSize = 30;
            waveLabel.color = new Color(1f, 0.9f, 0.6f);
            waveLabel.alignment = TextAlignmentOptions.Top;
            waveLabel.raycastTarget = false;

            WaveHUD waveHud = waveGo.AddComponent<WaveHUD>();
            SetField(waveHud, "waves", waveManager);
            SetField(waveHud, "gameManager", gameManager);
            SetField(waveHud, "label", waveLabel);

            // Centered message along the bottom edge ("Path blocked!"), invisible until shown.
            GameObject messageGo = new GameObject("MessageLabel", typeof(RectTransform));
            messageGo.transform.SetParent(canvasGo.transform, false);
            RectTransform messageRect = messageGo.GetComponent<RectTransform>();
            messageRect.anchorMin = messageRect.anchorMax = new Vector2(0.5f, 0f);
            messageRect.pivot = new Vector2(0.5f, 0f);
            messageRect.anchoredPosition = new Vector2(0f, 10f);
            messageRect.sizeDelta = new Vector2(900f, 70f);

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

            BuildGameOverPanel(canvasGo.transform, gameManager, resources, waveManager, grid, skin);

            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
            return messages;
        }

        private static WaveManager BuildWaveManager(GridManager grid, PlayerResources resources, Enemy enemyPrefab, List<WaveSO> waves)
        {
            GameObject go = new GameObject("WaveManager");
            WaveManager manager = go.AddComponent<WaveManager>();
            SetField(manager, "grid", grid);
            SetField(manager, "resources", resources);
            SetField(manager, "enemyPrefab", enemyPrefab);

            SerializedObject so = new SerializedObject(manager);
            SerializedProperty list = so.FindProperty("waves");
            list.arraySize = waves.Count;
            for (int i = 0; i < waves.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = waves[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return manager;
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
                case System.Enum e: prop.intValue = System.Convert.ToInt32(e); break;
                default: Debug.LogError($"[LevelBuilder] Unsupported type for '{fieldName}'"); break;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Assigns a serialized array of object references.
        private static void SetArray(Object target, string fieldName, Object[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogError($"[LevelBuilder] Field '{fieldName}' not found on {target.GetType().Name}"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] == null) Debug.LogError($"[LevelBuilder] Missing object in '{fieldName}'[{i}] on {target.GetType().Name}");
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetIntArray(Object target, string fieldName, int[] values)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null) { Debug.LogError($"[LevelBuilder] Field '{fieldName}' not found on {target.GetType().Name}"); return; }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).intValue = values[i];
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

        // Generates a white 2:1 diamond (tinted later via SpriteRenderer.color) with a slightly
        // darker rim, so neighbouring isometric tiles stay readable. Imported so that it is exactly
        // 1 world unit wide and 0.5 tall (pixels per unit = texture width).
        private static Sprite CreateDiamondSprite(string name, int widthPixels)
        {
            string path = $"{SpritesFolder}/{name}.png";
            int heightPixels = widthPixels / 2;
            Texture2D tex = new Texture2D(widthPixels, heightPixels, TextureFormat.RGBA32, false);
            float halfW = widthPixels * 0.5f;
            float halfH = heightPixels * 0.5f;
            Color rim = new Color(0.72f, 0.72f, 0.72f, 1f);
            for (int x = 0; x < widthPixels; x++)
            {
                for (int y = 0; y < heightPixels; y++)
                {
                    // Diamond test on pixel centers: |dx| / halfW + |dy| / halfH <= 1.
                    float d = Mathf.Abs(x + 0.5f - halfW) / halfW + Mathf.Abs(y + 0.5f - halfH) / halfH;
                    Color c = d > 1f ? Color.clear : (d > 0.88f ? rim : Color.white);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = widthPixels;
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Isometric tower body: a shaded pillar with a cap, so it reads as an object with height
        // (the diamond base plate is a separate sprite, see CreateTowerBaseSprite). White/grey so
        // SpriteRenderer.color tints it. 64 x 96 pixels, 1 world unit wide; pivot at the feet.
        private static Sprite CreateStandingTowerSprite(string name)
        {
            const int w = 64, h = 96, baseCy = 16;
            Texture2D tex = NewClearTexture(w, h);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    Color c = Color.clear;
                    if (px >= 20f && px < 44f && py >= baseCy && py < 70f)
                        c = px < 32f ? Color.white : new Color(0.74f, 0.74f, 0.74f); // lit left face, shaded right face
                    float capD = Mathf.Abs(px - 32f) / 12f + Mathf.Abs(py - 70f) / 6f;
                    if (capD <= 1f) c = capD > 0.8f ? new Color(0.8f, 0.8f, 0.8f) : new Color(0.93f, 0.93f, 0.93f);
                    tex.SetPixel(x, y, c);
                }
            }
            return SaveSprite(name, tex, w, new Vector2(0.5f, baseCy / (float)h), FilterMode.Bilinear);
        }

        // Isometric enemy: a small shaded sphere standing a little above its ground point.
        // Grey-scale shading (tinted by the EnemySO color): a soft highlight to the upper left and
        // a darker rim, so it reads as a ball. 64 x 80 pixels, 1 unit wide; the sphere fills the
        // top 64 pixels, pivot at the bottom (the feet).
        private static Sprite CreateStandingCircleSprite(string name)
        {
            const int w = 64, h = 80;
            const float radius = 31.5f;
            Texture2D tex = NewClearTexture(w, h);
            Vector2 center = new Vector2(32f, 48f);
            Vector2 light = new Vector2(-0.38f, 0.42f); // highlight position, in radius units
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    Vector2 n = (new Vector2(x + 0.5f, y + 0.5f) - center) / radius;
                    float d = n.magnitude;
                    float a = Mathf.Clamp01((1f - d) * radius + 0.5f); // soft 1-pixel edge
                    if (a <= 0f) continue;
                    float highlight = Mathf.Pow(Mathf.Clamp01(1f - (n - light).magnitude / 0.75f), 2f);
                    float rim = Mathf.Clamp01((d - 0.72f) / 0.28f);
                    float shade = Mathf.Lerp(0.86f, 0.58f, rim) + 0.14f * highlight + 0.25f * highlight * highlight;
                    tex.SetPixel(x, y, new Color(Mathf.Min(shade, 1f), Mathf.Min(shade, 1f), Mathf.Min(shade, 1f), a));
                }
            return SaveSprite(name, tex, w, new Vector2(0.5f, 0f), FilterMode.Bilinear);
        }

        // Isometric tower base plate: a grey diamond with a darker rim, 64 x 32 pixels = 1 x 0.5
        // units, pivot at its center (the feet). Separate from the pillar so the fire kick skips it.
        private static Sprite CreateTowerBaseSprite(string name)
        {
            const int w = 64, h = 32;
            Texture2D tex = NewClearTexture(w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    float d = Mathf.Abs(x + 0.5f - 32f) / 30f + Mathf.Abs(y + 0.5f - 16f) / 15f;
                    if (d <= 1f) tex.SetPixel(x, y, d > 0.85f ? new Color(0.42f, 0.42f, 0.42f) : new Color(0.55f, 0.55f, 0.55f));
                }
            return SaveSprite(name, tex, w, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        }

        // Thin white diamond outline (2:1), used for the hover outline drawn above the units.
        private static Sprite CreateDiamondOutlineSprite(string name, int widthPixels)
        {
            int heightPixels = widthPixels / 2;
            float halfW = widthPixels * 0.5f, halfH = heightPixels * 0.5f;
            Texture2D tex = NewClearTexture(widthPixels, heightPixels);
            for (int x = 0; x < widthPixels; x++)
                for (int y = 0; y < heightPixels; y++)
                {
                    float d = Mathf.Abs(x + 0.5f - halfW) / halfW + Mathf.Abs(y + 0.5f - halfH) / halfH;
                    if (d <= 1f && d >= 0.86f) tex.SetPixel(x, y, Color.white);
                }
            return SaveSprite(name, tex, widthPixels, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        }

        // Soft elliptical ground shadow, 2:1 (32 x 16 pixels = 1 x 0.5 units), tinted black at runtime.
        private static Sprite CreateShadowSprite(string name)
        {
            const int w = 32, h = 16;
            Texture2D tex = NewClearTexture(w, h);
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    float dx = (x + 0.5f - 16f) / 16f, dy = (y + 0.5f - 8f) / 8f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 1f) tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - d) * 2.5f)));
                }
            }
            return SaveSprite(name, tex, w, new Vector2(0.5f, 0.5f), FilterMode.Bilinear);
        }

        private static Texture2D NewClearTexture(int w, int h)
        {
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            Color[] clear = new Color[w * h];
            tex.SetPixels(clear);
            return tex;
        }

        // Writes the texture as a PNG and imports it as a single sprite with a custom pivot.
        private static Sprite SaveSprite(string name, Texture2D tex, int pixelsPerUnit, Vector2 pivot, FilterMode filter)
        {
            string path = $"{SpritesFolder}/{name}.png";
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = filter;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        // Creates the EnemySO asset, or updates it in place (same GUID) if it already exists.
        private static EnemySO CreateEnemyData(string name, float maxHealth, float moveSpeed, int goldReward, Color color, float scale,
            SkinSlot skinSlot, SkinSlot skinShadowSlot, int healthBarPixels)
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
            data.skinSlot = skinSlot;
            data.skinShadowSlot = skinShadowSlot;
            data.healthBarPixels = healthBarPixels;
            EditorUtility.SetDirty(data);
            return data;
        }

        // The 5 waves of the level. Counts/intervals were kept from M5: the balance was tuned
        // through enemy stats and rewards instead (see README / BalanceBot results).
        private static List<WaveSO> CreateWaves(EnemySO grunt, EnemySO tank)
        {
            return new List<WaveSO>
            {
                CreateWave("Wave1", Group(grunt, 5, 1.5f)),
                CreateWave("Wave2", Group(grunt, 8, 1.2f)),
                CreateWave("Wave3", Group(grunt, 6, 1.2f), Group(tank, 2, 3f, delayBefore: 2f)),
                CreateWave("Wave4", Group(grunt, 10, 1.0f), Group(tank, 4, 2.5f, delayBefore: 2f)),
                CreateWave("Wave5", Group(grunt, 12, 0.9f), Group(tank, 6, 2f, delayBefore: 2f)),
            };
        }

        private static WaveSO.SpawnGroup Group(EnemySO enemy, int count, float interval, float delayBefore = 0f)
        {
            return new WaveSO.SpawnGroup { enemy = enemy, count = count, spawnInterval = interval, delayBefore = delayBefore };
        }

        // Creates the WaveSO asset, or overwrites its groups in place (same GUID) if it exists.
        private static WaveSO CreateWave(string name, params WaveSO.SpawnGroup[] groups)
        {
            string path = $"{WavesFolder}/{name}.asset";
            WaveSO wave = AssetDatabase.LoadAssetAtPath<WaveSO>(path);
            if (wave == null)
            {
                wave = ScriptableObject.CreateInstance<WaveSO>();
                AssetDatabase.CreateAsset(wave, path);
            }
            wave.groups = new List<WaveSO.SpawnGroup>(groups);
            EditorUtility.SetDirty(wave);
            return wave;
        }

        // One generic enemy prefab (white circle); color/scale/stats come from EnemySO at spawn.
        private static void CreateEnemyPrefab(Sprite circle, Sprite square, Sprite standingCircle, Sprite shadowSprite, Material material,
            SkinSet skin)
        {
            // Root (logic, scaled per type at spawn) with the visual ball on a "Body" child at the
            // root's origin, so the isometric walking bob can move the ball alone.
            GameObject go = new GameObject("Enemy");
            Enemy enemy = go.AddComponent<Enemy>();
            GameObject bodyGo = CreateSpriteObject("Body", circle, material, Color.white, OrderEnemies);
            bodyGo.transform.SetParent(go.transform, false);
            SpriteRenderer body = bodyGo.GetComponent<SpriteRenderer>();
            SetField(enemy, "body", body);

            // Isometric only: a soft 2:1 ground shadow under the feet (hidden in top-down).
            GameObject shadow = CreateSpriteObject("Shadow", shadowSprite, material, new Color(0f, 0f, 0f, 0.7f), OrderEnemies - 1);
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localScale = Vector3.one * 1.15f;
            shadow.SetActive(false);
            SkinnedSprite shadowSkin = AddSkinnedSprite(shadow, SkinSlot.GruntShadow, skin, untinted: true, nativeSize: true);

            // Visuals: hit flash on the body, and a world-space health bar child (no Canvas).
            EnemyHitFlash flash = go.AddComponent<EnemyHitFlash>();
            SetField(flash, "body", body);

            GameObject bar = new GameObject("HealthBar");
            bar.transform.SetParent(go.transform, false);
            GameObject back = CreateSpriteObject("Background", square, material, new Color(0.08f, 0.08f, 0.1f, 0.85f), OrderHealthBarBack);
            back.transform.SetParent(bar.transform, false);
            GameObject fill = CreateSpriteObject("Fill", square, material, new Color(0.25f, 0.9f, 0.3f), OrderHealthBarFill);
            fill.transform.SetParent(bar.transform, false);
            back.GetComponent<SpriteRenderer>().enabled = false; // Hidden until the first hit.
            fill.GetComponent<SpriteRenderer>().enabled = false;

            EnemyHealthBar healthBar = bar.AddComponent<EnemyHealthBar>();
            SetField(healthBar, "enemy", enemy);
            SetField(healthBar, "background", back.GetComponent<SpriteRenderer>());
            SetField(healthBar, "fill", fill.GetComponent<SpriteRenderer>());
            SetField(healthBar, "body", body);

            // Look per projection: flat circle in top-down, standing circle + shadow in isometric.
            ProjectionLook look = go.AddComponent<ProjectionLook>();
            SetField(look, "target", body);
            SetField(look, "topDownSprite", circle);
            SetField(look, "isometricSprite", standingCircle);
            SetArray(look, "isometricOnly", new Object[] { shadow });
            SetField(look, "skin", skin);
            SetField(look, "isometricSlot", SkinSlot.Grunt); // Set per enemy type at spawn (EnemySO.skinSlot).
            SetField(look, "nativeSizeWhenSkinned", true);
            SetArray(look, "skinned", new Object[] { shadowSkin });
            SetField(healthBar, "look", look);
            SetField(enemy, "shadowSkin", shadowSkin);
            EnemyBob bob = go.AddComponent<EnemyBob>();
            SetField(bob, "enemy", enemy);
            SetField(bob, "ball", bodyGo.transform);

            // Isometric draw order: shadow < body < bar background < bar fill, all by depth.
            DepthSorter depth = go.AddComponent<DepthSorter>();
            SetArray(depth, "renderers", new Object[] { shadow.GetComponent<SpriteRenderer>(), body, back.GetComponent<SpriteRenderer>(), fill.GetComponent<SpriteRenderer>() });
            SetIntArray(depth, "parts", new[] { DepthSorter.PartShadow, DepthSorter.PartBody, DepthSorter.PartBarBack, DepthSorter.PartBarFill });

            SetField(enemy, "hitFlash", flash);
            SetField(enemy, "healthBar", healthBar);
            SetField(enemy, "look", look);
            SetField(enemy, "depth", depth);

            PrefabUtility.SaveAsPrefabAsset(go, EnemyPrefabPath); // Overwrites, keeps GUID.
            Object.DestroyImmediate(go);
        }

        // Creates the TowerSO asset, or updates it in place (same GUID) if it already exists.
        private static TowerSO CreateTowerData(string name, int cost, float range, float damage, float fireRate,
            float projectileSpeed, Color color)
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
            data.projectileSpeed = projectileSpeed;
            data.color = color;
            EditorUtility.SetDirty(data);
            return data;
        }

        // One generic tower prefab (white square, slightly smaller than a cell); colored from TowerSO.
        // Tower root (logic) with three visual children: Body (square in top-down, pillar in
        // isometric; the only part the fire kick scales), and, isometric only, Base (diamond plate)
        // and Shadow (soft ground shadow).
        private static void CreateTowerPrefab(Sprite square, Sprite standingTower, Sprite basePlate, Sprite shadowSprite, Material material,
            SkinSet skin)
        {
            GameObject go = new GameObject("Tower");
            go.transform.localScale = Vector3.one * CellSize * 0.8f;
            Tower tower = go.AddComponent<Tower>();

            GameObject bodyGo = CreateSpriteObject("Body", square, material, Color.white, OrderTowers);
            bodyGo.transform.SetParent(go.transform, false);
            SpriteRenderer body = bodyGo.GetComponent<SpriteRenderer>();
            SetField(tower, "body", body);

            GameObject baseGo = CreateSpriteObject("Base", basePlate, material, Color.white, OrderTowers - 1);
            baseGo.transform.SetParent(go.transform, false);
            baseGo.SetActive(false);

            GameObject shadowGo = CreateSpriteObject("Shadow", shadowSprite, material, new Color(0f, 0f, 0f, 0.6f), OrderTowers - 2);
            shadowGo.transform.SetParent(go.transform, false);
            shadowGo.transform.localScale = Vector3.one * 1.3f;
            shadowGo.SetActive(false);

            ProjectionLook look = go.AddComponent<ProjectionLook>();
            SetField(look, "target", body);
            SetField(look, "topDownSprite", square);
            SetField(look, "isometricSprite", standingTower);
            SetArray(look, "isometricOnly", new Object[] { baseGo, shadowGo });
            SetField(look, "skin", skin);
            SetField(look, "isometricSlot", SkinSlot.TowerBody);
            SetField(look, "nativeSizeWhenSkinned", true);
            SetArray(look, "hideWhenSkinned", new Object[] { baseGo }); // The skinned tower has its own stone base.
            SetArray(look, "skinned", new Object[]
            {
                AddSkinnedSprite(baseGo, SkinSlot.TowerBase, skin),
                AddSkinnedSprite(shadowGo, SkinSlot.TowerShadow, skin, untinted: true, nativeSize: true),
            });
            DepthSorter depth = go.AddComponent<DepthSorter>();
            SetArray(depth, "renderers", new Object[] { shadowGo.GetComponent<SpriteRenderer>(), baseGo.GetComponent<SpriteRenderer>(), body });
            SetIntArray(depth, "parts", new[] { 0, 1, 2 });
            SetField(tower, "look", look);
            SetField(tower, "depth", depth);
            SetField(tower, "projectilePrefab", AssetDatabase.LoadAssetAtPath<Projectile>(ProjectilePrefabPath));

            PrefabUtility.SaveAsPrefabAsset(go, TowerPrefabPath);
            Object.DestroyImmediate(go);
        }

        // Small yellow circle that homes in on its target.
        private static void CreateProjectilePrefab(Sprite circle, Material material, SkinSet skin)
        {
            GameObject go = CreateSpriteObject("Projectile", circle, material, ProjectileColor, OrderProjectiles);
            go.transform.localScale = Vector3.one * CellSize * 0.2f;
            Projectile projectile = go.AddComponent<Projectile>();
            DepthSorter depth = go.AddComponent<DepthSorter>();
            SetArray(depth, "renderers", new Object[] { go.GetComponent<SpriteRenderer>() });
            SetIntArray(depth, "parts", new[] { DepthSorter.PartBody });
            SetField(projectile, "depth", depth);
            SetField(projectile, "skinned", AddSkinnedSprite(go, SkinSlot.Projectile, skin, untinted: true, nativeSize: true));

            PrefabUtility.SaveAsPrefabAsset(go, ProjectilePrefabPath);
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

        // The isometric skin asset (same GUID on every rebuild): the terrain art is generated
        // (IsoArt) and every slot whose PNG exists is filled. Empty slots keep the M7 look.
        private static SkinSet CreateSkin()
        {
            IsoArt.GenerateAll(); // Isometric terrain PNGs (fixed seed: identical files on every build).
            SkinSet skin = SkinLoader.LoadOrCreate(SkinPath);
            int filled = SkinLoader.Fill(skin);
            int period = IsoArt.GrassPeriod;
            skin.grassPeriod = skin.grassTiles.Length == period * period ? period : 0;
            EditorUtility.SetDirty(skin);
            Debug.Log($"[LevelBuilder] Skin {SkinPath}: {filled} sprite(s) from {PixelArt.GeneratedFolder}.");
            return skin;
        }

        // Lets a prefab part take its isometric sprite from 'slot' (its own sprite is the fallback).
        // untinted: a filled slot is drawn white (pixel art has its own colours);
        // nativeSize: a filled slot is drawn at 64 px per unit whatever the parents' scale.
        private static SkinnedSprite AddSkinnedSprite(GameObject go, SkinSlot slot, SkinSet skin, bool untinted = false, bool nativeSize = false)
        {
            SkinnedSprite skinned = go.AddComponent<SkinnedSprite>();
            SetField(skinned, "skin", skin);
            SetField(skinned, "slot", slot);
            SetField(skinned, "spriteRenderer", go.GetComponent<SpriteRenderer>());
            SetField(skinned, "whiteWhenSkinned", untinted);
            SetField(skinned, "nativeSize", nativeSize);
            return skinned;
        }

        // Same for a UI Image; it follows the projection by itself (V switch) through 'grid'.
        private static void AddSkinnedImage(GameObject go, Image image, SkinSlot slot, SkinSet skin, GridManager grid)
        {
            SkinnedSprite skinned = go.AddComponent<SkinnedSprite>();
            SetField(skinned, "skin", skin);
            SetField(skinned, "slot", slot);
            SetField(skinned, "image", image);
            SetField(skinned, "grid", grid);
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
