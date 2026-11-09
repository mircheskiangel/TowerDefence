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
        private const string LevelsFolder = DataFolder + "/Levels";
        private const string Level1Path = LevelsFolder + "/Level1.asset";
        private const string Level2Path = LevelsFolder + "/Level2.asset";
        private const string Level3Path = LevelsFolder + "/Level3.asset";
        private const string Level4Path = LevelsFolder + "/Level4.asset";
        private static readonly string[] LevelPaths = { Level1Path, Level2Path, Level3Path, Level4Path };
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

        // --- Level 2 "The Snake" (tuned with BalanceBot; Level 1 and the enemy base stats stay as they are) ---
        private const int Level2Gold = 200;
        private const int Level2Lives = 10;
        private const int Level2DecorSeed = 23;
        private const float Wave2TankHealth = 1.3f;
        private const float EliteTankHealth = 2.0f;

        // --- Level 3 "The Fortress" (tuned with BalanceBot; everything else stays as it is) ---
        private const int Level3Gold = 150;
        private const int Level3Lives = 10;
        private const int Level3DecorSeed = 37;
        private const float Level3Wave2TankHealth = 1.3f;
        private const float Level3EliteTankHealth = 2.2f;

        // --- Level 4 "The Gauntlet" (tuned with BalanceBot; everything else stays as it is) ---
        private const int Level4Gold = 225;
        private const int Level4Lives = 10;
        private const int Level4DecorSeed = 53;
        private const int Level4SiegeLimit = 2;
        private const float Level4Wave2TankHealth = 1.3f;
        private const float Level4Wave3TankHealth = 1.5f;
        private const float Level4EliteTankHealth = 2.0f;

        // The most tower cards a level can show (one per allowed tower).
        private const int MaxTowerCards = 2;

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
            EnsureFolder(LevelsFolder);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ArtFolder);

            // 1. Assets
            SkinSet skin = CreateSkin();
            Sprite square = CreateSprite("Square", SpriteShape.Square);
            Sprite circle = CreateSprite("Circle", SpriteShape.Circle);
            Sprite ring = CreateSprite("RangeRing", SpriteShape.Ring, RingPixels);
            Sprite wallSquare = CreateSprite("WallSquare", SpriteShape.RoundedSquare);
            Sprite diamond = CreateDiamondSprite("Diamond", DiamondPixelsWide);
            Sprite towerIso = CreateStandingTowerSprite("TowerIso");
            Sprite towerBase = CreateTowerBaseSprite("TowerIsoBase");
            Sprite diamondOutline = CreateDiamondOutlineSprite("DiamondOutline", DiamondPixelsWide);
            Sprite enemyIso = CreateStandingCircleSprite("EnemyIso");
            Sprite shadow = CreateShadowSprite("Shadow");
            Material spriteMaterial = CreateSpriteMaterial();
            EnemySO grunt = CreateEnemyData("Grunt", maxHealth: 76f, moveSpeed: 2.3f, goldReward: 4,
                new Color(1.00f, 0.80f, 0.20f), scale: 0.5f, SkinSlot.Grunt, SkinSlot.GruntShadow, IsoArt.GruntBarPixels,
                SkinSlot.GruntWalk, SkinSlot.GruntCharacterShadow, walkFrameSeconds: 0.18f);
            EnemySO tank = CreateEnemyData("Tank", maxHealth: 400f, moveSpeed: 1.1f, goldReward: 10,
                new Color(0.65f, 0.35f, 0.90f), scale: 0.8f, SkinSlot.Tank, SkinSlot.TankShadow, IsoArt.TankBarPixels,
                SkinSlot.TankWalk, SkinSlot.TankCharacterShadow, walkFrameSeconds: 0.30f);
            // The elite look of the Tank. Visual only: its strength is the wave group's health multiplier.
            tank.eliteSkinSlot = SkinSlot.TankElite;
            tank.eliteSkinShadowSlot = SkinSlot.TankEliteShadow;
            tank.eliteWalkSlot = SkinSlot.TankEliteWalk;
            tank.eliteCharacterShadowSlot = SkinSlot.TankEliteCharacterShadow;
            tank.eliteColor = new Color(0.40f, 0.18f, 0.62f);
            tank.eliteScale = IsoArt.EliteScale;
            tank.eliteHealthBarPixels = IsoArt.TankEliteBarPixels;
            EditorUtility.SetDirty(tank);
            CreateEnemyPrefab(circle, square, enemyIso, shadow, spriteMaterial, skin);
            List<WaveSO> waves = CreateWaves(grunt, tank);
            TowerSO basicTower = CreateTowerData("Basic Tower", cost: 25, range: 2.5f, damage: 10f, fireRate: 1.5f,
                projectileSpeed: 8f, BasicTowerColor);
            basicTower.projectileColor = ProjectileColor;
            // The Siege Cannon's stats are fixed by design: damage 50, range 4.5, 3 s between shots.
            TowerSO siegeCannon = CreateTowerData("Siege Cannon", cost: 50, range: 4.5f, damage: 50f, fireRate: 1f / 3f,
                projectileSpeed: 6f, new Color(0.34f, 0.20f, 0.24f));
            siegeCannon.bodySlot = SkinSlot.SiegeBodyPlain;
            siegeCannon.fixedBodySlot = SkinSlot.SiegeBody;
            siegeCannon.shadowSlot = SkinSlot.SiegeShadow;
            siegeCannon.projectileSlot = SkinSlot.SiegeProjectile;
            siegeCannon.projectileColor = new Color(0.30f, 0.20f, 0.20f);
            siegeCannon.projectileScale = 1.6f;
            siegeCannon.projectileTrail = true;
            siegeCannon.fireKickScale = 1.16f;
            siegeCannon.topDownScale = 1.15f;
            EditorUtility.SetDirty(siegeCannon);
            var onlyBasic = new List<TowerSO> { basicTower };
            CreateLevel(Level1Path, "The Meadow", new List<Vector2Int>(), StartGold, StartLives, waves, DecorSeed, onlyBasic, new List<LevelData.FixedTower>());
            CreateLevel(Level2Path, "The Snake", SnakeWalls(), Level2Gold, Level2Lives, CreateLevel2Waves(grunt, tank), Level2DecorSeed, onlyBasic, new List<LevelData.FixedTower>());
            CreateLevel(Level3Path, "The Fortress", FortressWalls(), Level3Gold, Level3Lives, CreateLevel3Waves(grunt, tank), Level3DecorSeed, onlyBasic,
                new List<LevelData.FixedTower>
                {
                    new LevelData.FixedTower { tower = siegeCannon, cell = new Vector2Int(4, 7) },
                    new LevelData.FixedTower { tower = siegeCannon, cell = new Vector2Int(12, 7) },
                });
            CreateLevel(Level4Path, "The Gauntlet", GauntletWalls(), Level4Gold, Level4Lives, CreateLevel4Waves(grunt, tank), Level4DecorSeed,
                new List<TowerSO> { basicTower, siegeCannon },
                new List<LevelData.FixedTower>
                {
                    new LevelData.FixedTower { tower = siegeCannon, cell = new Vector2Int(1, 8) },
                    new LevelData.FixedTower { tower = siegeCannon, cell = new Vector2Int(13, 8) },
                },
                new List<LevelData.TowerLimit> { new LevelData.TowerLimit { tower = siegeCannon, maxCount = Level4SiegeLimit } });
            AssertLevelsAreValid();
            CreateProjectilePrefab(circle, spriteMaterial, skin); // Before the tower prefab, which references it.
            CreateTowerPrefab(square, circle, towerIso, towerBase, shadow, spriteMaterial, skin);

            // 2. Fresh scene (replaces any previous build)
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            skin = AssetDatabase.LoadAssetAtPath<SkinSet>(SkinPath); // Re-loaded, like the prefabs below.

            // 3. Grid
            GameObject gridGo = new GameObject("Grid");
            GridManager grid = gridGo.AddComponent<GridManager>();
            LevelData level1 = AssetDatabase.LoadAssetAtPath<LevelData>(Level1Path);
            LevelData level2 = AssetDatabase.LoadAssetAtPath<LevelData>(Level2Path);
            LevelData level3 = AssetDatabase.LoadAssetAtPath<LevelData>(Level3Path);
            LevelData level4 = AssetDatabase.LoadAssetAtPath<LevelData>(Level4Path);
            grid.Configure(level1.width, level1.height, CellSize, Vector2.zero, level1.spawnCell, level1.goalCell, startProjection);
            BoardView board = BuildBoard(grid, square, diamond, wallSquare, spriteMaterial, skin, level1);

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
            TowerPlacer placer = BuildTowerPlacer(grid, resources, towerPrefab, basicTower, square, diamond, diamondOutline, ring, spriteMaterial, messages, gameManager, skin);
            BuildTowerSelector(messages.transform.parent, grid, resources, placer, skin);
            BuildLevelManager(new[] { level1, level2, level3, level4 }, grid, resources, waveManager, placer, board, gameManager);
            BuildAStarOverlay(grid, pathView, messages, square, diamond, spriteMaterial);
            new GameObject("TargetingModeSwitcher").AddComponent<TargetingModeSwitcher>();
            ProjectionSwitcher switcher = new GameObject("ProjectionSwitcher").AddComponent<ProjectionSwitcher>();
            SetField(switcher, "grid", grid);
            new GameObject("EnemySkinSwitcher").AddComponent<EnemySkinSwitcher>();

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
        private static BoardView BuildBoard(GridManager grid, Sprite square, Sprite diamond, Sprite wallSquare, Material material, SkinSet skin, LevelData level)
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
            SetField(board, "wallSquareSprite", wallSquare);
            SetField(board, "wallSquareOrder", OrderTowers);
            SetField(board, "bakedLevel", level);
            SetDecorations(board, DecorationPlacer.Place(grid, level.decorationSeed, RingWidth, IsoArt.TreeVariants, IsoArt.RockVariants, IsoArt.BushVariants));
            board.Rebuild();
            return board;
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
        }

        // Applies the selected level at the start of the scene. Goes first in the execution order.
        private static void BuildLevelManager(LevelData[] levels, GridManager grid, PlayerResources resources, WaveManager waves,
            TowerPlacer placer, BoardView board, GameManager gameManager)
        {
            LevelManager manager = new GameObject("LevelManager").AddComponent<LevelManager>();
            SetArray(manager, "levels", levels);
            SetField(manager, "grid", grid);
            SetField(manager, "resources", resources);
            SetField(manager, "waves", waves);
            SetField(manager, "placer", placer);
            SetField(manager, "board", board);
            SetField(manager, "gameManager", gameManager);
        }

        // The O key: overlay of the cells the last spawn -> goal A* search expanded.
        private static void BuildAStarOverlay(GridManager grid, PathDebugView pathView, MessageDisplay messages, Sprite square, Sprite diamond, Material material)
        {
            AStarOverlay overlay = new GameObject("AStarOverlay").AddComponent<AStarOverlay>();
            SetField(overlay, "grid", grid);
            SetField(overlay, "pathView", pathView);
            SetField(overlay, "messages", messages);
            SetField(overlay, "squareSprite", square);
            SetField(overlay, "diamondSprite", diamond);
            SetField(overlay, "material", material);
        }

        // ---------------------------------------------------------------- Levels

        // Creates the LevelData asset, or updates it in place (same GUID) if it already exists.
        private static LevelData CreateLevel(string path, string levelName, List<Vector2Int> walls, int gold, int lives, List<WaveSO> waves, int decorSeed,
            List<TowerSO> allowedTowers, List<LevelData.FixedTower> fixedTowers, List<LevelData.TowerLimit> towerLimits = null)
        {
            LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
            if (level == null)
            {
                level = ScriptableObject.CreateInstance<LevelData>();
                AssetDatabase.CreateAsset(level, path);
            }
            level.levelName = levelName;
            level.width = GridWidth;
            level.height = GridHeight;
            level.spawnCell = SpawnCell;
            level.goalCell = GoalCell;
            level.wallCells = walls;
            level.startingGold = gold;
            level.startingLives = lives;
            level.waves = waves;
            level.decorationSeed = decorSeed;
            level.allowedTowers = new List<TowerSO>(allowedTowers);
            level.fixedTowers = fixedTowers;
            level.towerLimits = towerLimits ?? new List<LevelData.TowerLimit>();
            EditorUtility.SetDirty(level);
            return level;
        }

        // Level 2: 12 wall cells in three short groups that change the path's direction several times.
        private static List<Vector2Int> SnakeWalls()
        {
            var walls = new List<Vector2Int>();
            for (int y = 3; y <= 6; y++) walls.Add(new Vector2Int(4, y));
            for (int y = 4; y <= 7; y++) walls.Add(new Vector2Int(8, y));
            for (int y = 1; y <= 4; y++) walls.Add(new Vector2Int(11, y));
            return walls;
        }

        // Level 3: 16 wall cells in three lines with gates; two Siege Cannons cover the upper side.
        private static List<Vector2Int> FortressWalls()
        {
            var walls = new List<Vector2Int>();
            foreach (int y in new[] { 1, 2, 3, 6, 7, 8 }) walls.Add(new Vector2Int(3, y));
            for (int y = 3; y <= 6; y++) walls.Add(new Vector2Int(7, y));
            foreach (int y in new[] { 1, 2, 3, 6, 7, 8 }) walls.Add(new Vector2Int(11, y));
            return walls;
        }

        // Level 4: 15 wall cells in four short lines; fixed Siege Cannons cover the start and the end.
        private static List<Vector2Int> GauntletWalls()
        {
            var walls = new List<Vector2Int>();
            for (int y = 2; y <= 5; y++) walls.Add(new Vector2Int(3, y));
            for (int y = 4; y <= 8; y++) walls.Add(new Vector2Int(7, y));
            for (int y = 1; y <= 4; y++) walls.Add(new Vector2Int(11, y));
            for (int y = 6; y <= 7; y++) walls.Add(new Vector2Int(13, y));
            return walls;
        }

        // Level 4: 3 waves; the third has only Tanks, which is what the Siege Cannons are for.
        private static List<WaveSO> CreateLevel4Waves(EnemySO grunt, EnemySO tank)
        {
            return new List<WaveSO>
            {
                CreateWave("Level4Wave1", Group(grunt, 11, 0.95f), Group(tank, 2, 3.0f, delayBefore: 2f)),
                CreateWave("Level4Wave2", Group(grunt, 15, 0.85f), Group(tank, 4, 2.5f, delayBefore: 2f, healthMultiplier: Level4Wave2TankHealth)),
                CreateWave("Level4Wave3", Group(tank, 5, 2.0f, healthMultiplier: Level4Wave3TankHealth), Group(tank, 7, 2.0f, delayBefore: 3f, healthMultiplier: Level4EliteTankHealth, elite: true)),
            };
        }

        // Level 3: 3 waves, each bigger and harder than the Level 2 equivalent.
        private static List<WaveSO> CreateLevel3Waves(EnemySO grunt, EnemySO tank)
        {
            return new List<WaveSO>
            {
                CreateWave("Level3Wave1", Group(grunt, 11, 0.95f), Group(tank, 2, 3.0f, delayBefore: 2f)),
                CreateWave("Level3Wave2", Group(grunt, 15, 0.9f), Group(tank, 4, 2.5f, delayBefore: 2f, healthMultiplier: Level3Wave2TankHealth)),
                CreateWave("Level3Wave3", Group(grunt, 21, 0.65f), Group(tank, 8, 2.0f, delayBefore: 2f, healthMultiplier: Level3EliteTankHealth, elite: true)),
            };
        }

        // Level 2: 3 waves, bigger and stronger than Level 1. The last Tanks of each level ramp up
        // through the health multiplier; base stats are the same as everywhere.
        private static List<WaveSO> CreateLevel2Waves(EnemySO grunt, EnemySO tank)
        {
            return new List<WaveSO>
            {
                CreateWave("Level2Wave1", Group(grunt, 10, 1.0f), Group(tank, 2, 3.0f, delayBefore: 2f)),
                CreateWave("Level2Wave2", Group(grunt, 14, 0.9f), Group(tank, 4, 2.5f, delayBefore: 2f, healthMultiplier: Wave2TankHealth)),
                CreateWave("Level2Wave3", Group(grunt, 20, 0.7f), Group(tank, 7, 2.0f, delayBefore: 2f, healthMultiplier: EliteTankHealth, elite: true)),
            };
        }

        // Every level must have a spawn -> goal path with its walls and fixed towers in place, and may
        // only offer towers the player can buy. Throws, so a broken level stops the build before
        // anything is saved.
        private static void AssertLevelsAreValid()
        {
            GameObject temp = new GameObject("LevelCheck");
            try
            {
                GridManager grid = temp.AddComponent<GridManager>();
                foreach (string path in LevelPaths)
                {
                    LevelData level = AssetDatabase.LoadAssetAtPath<LevelData>(path);
                    grid.ApplyLevel(level);
                    foreach (Vector2Int wall in level.wallCells)
                        if (!grid.IsInBounds(wall) || wall == level.spawnCell || wall == level.goalCell)
                            throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': bad wall cell {wall}.");
                    foreach (LevelData.FixedTower tower in level.fixedTowers)
                        if (tower.tower == null || !grid.IsInBounds(tower.cell) || tower.cell == level.spawnCell || tower.cell == level.goalCell || level.wallCells.Contains(tower.cell))
                            throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': bad fixed tower at {tower.cell}.");
                    if (level.allowedTowers.Count == 0)
                        throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': no tower to buy.");
                    foreach (TowerSO tower in level.allowedTowers)
                        if (tower == null)
                            throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': an allowed tower is missing.");
                    if (level.allowedTowers.Count > MaxTowerCards)
                        throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': more allowed towers than tower cards.");
                    foreach (LevelData.TowerLimit limit in level.towerLimits)
                        if (limit.maxCount < 1 || !level.allowedTowers.Contains(limit.tower))
                            throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': bad tower limit.");
                    List<Vector2Int> route = new AStarPathfinder(grid).FindPath(level.spawnCell, level.goalCell);
                    if (route == null)
                        throw new System.InvalidOperationException($"[LevelBuilder] Level '{level.levelName}': A* finds NO path from {level.spawnCell} to {level.goalCell}.");
                    Debug.Log($"[LevelBuilder] Level '{level.levelName}': path of {route.Count} cells including start and goal.");
                }
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        [MenuItem("Tower Defense/Unlock All Levels")]
        public static void UnlockAllLevels()
        {
            LevelProgress.UnlockAll(LevelPaths.Length);
            Debug.Log("[LevelBuilder] All levels unlocked.");
        }

        [MenuItem("Tower Defense/Reset Progress")]
        public static void ResetProgress()
        {
            LevelProgress.ResetProgress();
            Debug.Log("[LevelBuilder] Progress reset: only Level 1 is unlocked.");
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

        private static TowerPlacer BuildTowerPlacer(GridManager grid, PlayerResources resources, Tower towerPrefab,
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
            SetField(indicator, "fillColor", new Color(1f, 1f, 1f, 0.2f));
            SetField(indicator, "lineColor", new Color(1f, 1f, 1f, 0.6f));
            SetField(indicator, "lineWidth", 0.022f);

            // Isometric skin only: semi-transparent preview of the tower on a valid hovered cell.
            GameObject preview = CreateSpriteObject("PreviewTower", square, material, Color.white, OrderTowers);
            preview.transform.SetParent(go.transform, false);
            preview.GetComponent<SpriteRenderer>().enabled = false;
            SetField(placer, "previewTower", preview.GetComponent<SpriteRenderer>());
            SetField(placer, "skin", skin);
            SetField(placer, "puffMaterial", material);
            SetField(placer, "rangeIndicator", indicator);
            SetField(placer, "messages", messages);
            SetField(placer, "gameManager", gameManager);
            return placer;
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
        // Returns the isometric frame (shown by IsoHud only with the isometric HUD).
        private static GameObject BuildGameOverPanel(Transform canvas, GameManager gameManager, PlayerResources resources, WaveManager waveManager,
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

            // Isometric HUD style: a dark metal-framed box behind title, result and button (the
            // full-screen dim stays in both views).
            Image frame = UiImage("Frame", panel.transform, skin.hudPanel, Color.white, true);
            SetRect(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 440f));
            frame.gameObject.SetActive(false);

            TextMeshProUGUI title = CreateCenteredText("Title", panel.transform, "GAME OVER", 110, new Vector2(0f, 110f), new Vector2(1200f, 150f));
            title.fontStyle = FontStyles.Bold;
            TextMeshProUGUI detail = CreateCenteredText("Detail", panel.transform, "", 44, new Vector2(0f, 10f), new Vector2(1200f, 70f));

            Button button = CreatePanelButton("RestartButton", panel.transform, "Restart (R)", skin, grid, out _);
            Button nextButton = CreatePanelButton("NextButton", panel.transform, "Next level", skin, grid, out TextMeshProUGUI nextText);
            nextButton.gameObject.SetActive(false); // Shown only on a win, beside Restart.

            panel.SetActive(false);

            GameOverPanel script = root.AddComponent<GameOverPanel>();
            SetField(script, "gameManager", gameManager);
            SetField(script, "resources", resources);
            SetField(script, "waves", waveManager);
            SetField(script, "panel", panel);
            SetField(script, "title", title);
            SetField(script, "detail", detail);
            SetField(script, "restartButton", button);
            SetField(script, "nextButton", nextButton);
            SetField(script, "nextLabel", nextText);
            return frame.gameObject;
        }

        // A button of the Win/Lose panel. Its look follows the view (HudButton slot in isometric).
        private static Button CreatePanelButton(string name, Transform parent, string text, SkinSet skin, GridManager grid, out TextMeshProUGUI label)
        {
            GameObject buttonGo = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(parent, false);
            RectTransform buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -110f);
            buttonRect.sizeDelta = new Vector2(360f, 96f);
            Image buttonImage = buttonGo.GetComponent<Image>();
            buttonImage.color = new Color(0.30f, 0.75f, 0.95f);
            buttonImage.type = Image.Type.Sliced; // No effect without a sprite (top-down look unchanged).
            buttonImage.pixelsPerUnitMultiplier = UiPixelMultiplier;
            AddSkinnedImage(buttonGo, buttonImage, SkinSlot.HudButton, skin, grid);
            Button button = buttonGo.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            label = CreateCenteredText("Text", buttonGo.transform, text, 42, Vector2.zero, Vector2.zero);
            StretchFull(label.rectTransform);
            label.color = new Color(0.08f, 0.09f, 0.12f);
            label.raycastTarget = false;
            return button;
        }

        // The level select screen, styled with the isometric HUD panels (in both views): a dark
        // full-screen layer, a metal frame, and one button per level with a lock and a hint that
        // LevelSelectPanel shows while the level is locked.
        private static void BuildLevelSelect(Transform canvas, SkinSet skin)
        {
            RectTransform root = UiRect("LevelSelectUI", canvas);
            StretchFull(root);
            RectTransform panel = UiRect("Panel", root);
            StretchFull(panel);
            Image dim = panel.gameObject.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.6f);
            dim.raycastTarget = true; // Blocks clicks from reaching the grid behind it.

            Image frame = UiImage("Frame", panel, skin.hudPanel, Color.white, true);
            SetRect(frame.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(860f, 850f));
            TextMeshProUGUI title = UiText("Title", frame.rectTransform, "SELECT LEVEL", 60, HudTextColor, TextAlignmentOptions.Center, new Vector2(0f, 345f), new Vector2(760f, 90f), centered: true);
            title.raycastTarget = false;

            int count = LevelPaths.Length;
            Button[] buttons = new Button[count];
            Object[] labels = new Object[count], locks = new Object[count], hints = new Object[count];
            for (int i = 0; i < count; i++)
            {
                float y = 215f - i * 155f;
                Image image = UiImage($"Level{i + 1}", frame.rectTransform, skin.hudButton, Color.white, true);
                image.raycastTarget = true;
                SetRect(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, y), new Vector2(700f, 110f));
                Button button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                ColorBlock colors = button.colors;
                colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f);
                button.colors = colors;
                TextMeshProUGUI label = UiText("Name", image.rectTransform, "", 44, new Color(0.08f, 0.09f, 0.12f), TextAlignmentOptions.Center, Vector2.zero, new Vector2(520f, 90f), centered: true);
                Image lockIcon = UiImage("Lock", image.rectTransform, skin.iconLock, Color.white, false);
                SetRect(lockIcon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(28f, 0f), new Vector2(56f, 70f));
                TextMeshProUGUI hint = UiText("Hint", frame.rectTransform, $"Win Level {i} to unlock", 30, HudGoldColor, TextAlignmentOptions.Center, new Vector2(0f, y - 74f), new Vector2(700f, 40f), centered: true);
                lockIcon.gameObject.SetActive(false);
                hint.gameObject.SetActive(false);
                buttons[i] = button;
                labels[i] = label;
                locks[i] = lockIcon.gameObject;
                hints[i] = hint.gameObject;
            }
            panel.gameObject.SetActive(false);

            LevelSelectPanel script = root.gameObject.AddComponent<LevelSelectPanel>();
            SetField(script, "panel", panel.gameObject);
            SetArray(script, "buttons", buttons);
            SetArray(script, "labels", labels);
            SetArray(script, "lockIcons", locks);
            SetArray(script, "lockHints", hints);
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

            GameObject gameOverFrame = BuildGameOverPanel(canvasGo.transform, gameManager, resources, waveManager, grid, skin);
            BuildIsoHud(canvasGo, resources, waveManager, gameManager, messages, grid, skin,
                new[] { labelGo, targetingGo, waveGo, messageGo }, gameOverFrame);
            BuildLevelSelect(canvasGo.transform, skin);

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

        private enum SpriteShape { Square, Circle, Ring, RoundedSquare }

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
                    float brightness = 1f;
                    switch (shape)
                    {
                        case SpriteShape.Square: alpha = 1f; break;
                        case SpriteShape.RoundedSquare:
                        {
                            // Rounded corners and a darker rim, tinted stone at runtime.
                            float corner = size * 0.22f;
                            Vector2 q = new Vector2(Mathf.Abs(x - center.x), Mathf.Abs(y - center.y)) - Vector2.one * (radius - corner);
                            float edge = Mathf.Max(q.x, 0f) * Mathf.Max(q.x, 0f) + Mathf.Max(q.y, 0f) * Mathf.Max(q.y, 0f);
                            float outside = Mathf.Sqrt(edge) + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;
                            alpha = outside <= 0f ? 1f : 0f;
                            brightness = outside > -size * 0.09f ? 0.68f : 1f;
                            break;
                        }
                        case SpriteShape.Circle: alpha = d <= radius ? 1f : 0f; break;
                        default: alpha = d > radius ? 0f : (d >= radius - ringWidth ? 1f : 0.12f); break;
                    }
                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness, alpha));
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
            SkinSlot skinSlot, SkinSlot skinShadowSlot, int healthBarPixels,
            SkinSlot walkSlot, SkinSlot characterShadowSlot, float walkFrameSeconds)
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
            data.walkSlot = walkSlot;
            data.characterShadowSlot = characterShadowSlot;
            data.walkFrameSeconds = walkFrameSeconds;
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

        private static WaveSO.SpawnGroup Group(EnemySO enemy, int count, float interval, float delayBefore = 0f, float healthMultiplier = 1f, bool elite = false)
        {
            return new WaveSO.SpawnGroup { enemy = enemy, count = count, spawnInterval = interval, delayBefore = delayBefore, healthMultiplier = healthMultiplier, elite = elite };
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
            SetField(healthBar, "skin", skin);
            SetField(enemy, "shadowSkin", shadowSkin);
            EnemyBob bob = go.AddComponent<EnemyBob>();
            SetField(bob, "enemy", enemy);
            SetField(bob, "ball", bodyGo.transform);
            EnemyWalkAnimator walk = go.AddComponent<EnemyWalkAnimator>();
            SetField(walk, "enemy", enemy);
            SetField(walk, "look", look);
            SetField(walk, "body", body);
            SetField(walk, "skin", skin);

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
        private static void CreateTowerPrefab(Sprite square, Sprite circle, Sprite standingTower, Sprite basePlate, Sprite shadowSprite, Material material,
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

            // Top-down only: the gold dot of a fixed tower, not under the body so the kick never scales it.
            GameObject markGo = CreateSpriteObject("FixedMark", circle, material, new Color(1f, 0.82f, 0.25f), OrderTowers + 1);
            markGo.transform.SetParent(go.transform, false);
            markGo.transform.localScale = Vector3.one * 0.3f;
            markGo.SetActive(false);
            SetField(tower, "fixedMark", markGo);

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
            SkinnedSprite shadowSkin = AddSkinnedSprite(shadowGo, SkinSlot.TowerShadow, skin, untinted: true, nativeSize: true);
            SetField(tower, "shadowSkin", shadowSkin);
            SetArray(look, "skinned", new Object[]
            {
                AddSkinnedSprite(baseGo, SkinSlot.TowerBase, skin),
                shadowSkin,
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
            // A short trail, off unless the tower type asks for it (the Siege Cannon's heavy shot).
            TrailRenderer trail = go.AddComponent<TrailRenderer>();
            trail.enabled = false;
            trail.sharedMaterial = material;
            trail.time = 0.16f;
            trail.startWidth = 0.13f;
            trail.endWidth = 0.02f;
            trail.startColor = new Color(0.40f, 0.20f, 0.14f, 0.85f);
            trail.endColor = new Color(0.40f, 0.20f, 0.14f, 0f);
            trail.numCapVertices = 2;
            trail.sortingOrder = OrderProjectiles;
            SetField(projectile, "trail", trail);
            DepthSorter depth = go.AddComponent<DepthSorter>();
            SetArray(depth, "renderers", new Object[] { trail, go.GetComponent<SpriteRenderer>() });
            SetIntArray(depth, "parts", new[] { DepthSorter.PartShadow, DepthSorter.PartBody });
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

        // UI pixel art: one sprite pixel = 3 canvas units at the 1920 x 1080 reference size
        // (canvas 100 units per unit / sprite 64 px per unit / multiplier 0.52 ~= 3).
        private const float UiPixelMultiplier = 0.52f;
        private static readonly Color HudTextColor = new Color(0.93f, 0.91f, 0.85f);
        private static readonly Color HudGoldColor = new Color(1f, 0.84f, 0.35f);
        private static readonly Color HudLivesColor = new Color(1f, 0.55f, 0.58f);
        private static readonly Color HudArrowButtonColor = new Color(0.42f, 0.47f, 0.56f);

        // The isometric HUD (dark metal panels with icons), shown by IsoHud instead of the
        // original labels in isometric. Top row like the original layout: gold and lives left,
        // wave centre, targeting selector right; message panel bottom centre.
        private static void BuildIsoHud(GameObject canvasGo, PlayerResources resources, WaveManager waveManager, GameManager gameManager,
            MessageDisplay messages, GridManager grid, SkinSet skin, GameObject[] topDownLabels, GameObject gameOverFrame)
        {
            RectTransform root = UiRect("IsoHUD", canvasGo.transform);
            StretchFull(root);
            root.SetAsFirstSibling(); // Under the Game Over overlay.

            // Gold (coin) and lives (heart), top left.
            RectTransform gold = UiPanel("GoldPanel", root, skin, new Vector2(0f, 1f), new Vector2(20f, -12f), new Vector2(200f, 64f));
            UiIcon("Coin", gold, skin.iconCoin, new Vector2(20f, 0f), 36f);
            TextMeshProUGUI goldText = UiText("Value", gold, "0", 36, HudGoldColor, TextAlignmentOptions.Left, new Vector2(66f, 0f), new Vector2(124f, 56f));
            RectTransform lives = UiPanel("LivesPanel", root, skin, new Vector2(0f, 1f), new Vector2(232f, -12f), new Vector2(170f, 64f));
            UiIcon("Heart", lives, skin.iconHeart, new Vector2(20f, 0f), 36f);
            TextMeshProUGUI livesText = UiText("Value", lives, "0", 36, HudLivesColor, TextAlignmentOptions.Left, new Vector2(68f, 0f), new Vector2(94f, 56f));

            // Wave, top centre.
            // Taller than the other panels, for the level name under the wave text (which stays in the same row).
            RectTransform wave = UiPanel("WavePanel", root, skin, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(300f, 90f));
            TextMeshProUGUI waveText = UiText("Value", wave, "WAVE 1 / 5", 34, HudTextColor, TextAlignmentOptions.Center, Vector2.zero, new Vector2(280f, 56f), centered: true);
            SetRect(waveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(280f, 56f));
            TextMeshProUGUI levelText = UiText("Level", wave, "", 20, HudGoldColor, TextAlignmentOptions.Center, Vector2.zero, new Vector2(280f, 26f), centered: true);
            SetRect(levelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -68f), new Vector2(280f, 26f));

            // Targeting selector, top right: [<] TARGET: FIRST [>].
            RectTransform target = UiPanel("TargetPanel", root, skin, new Vector2(1f, 1f), new Vector2(-20f, -12f), new Vector2(470f, 64f));
            Button previous = UiArrowButton("Previous", target, skin, new Vector2(0f, 0.5f), new Vector2(8f, 0f), false);
            Button next = UiArrowButton("Next", target, skin, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), true);
            TextMeshProUGUI targetText = UiText("Value", target, "TARGET: FIRST", 30, HudTextColor, TextAlignmentOptions.Center, Vector2.zero, new Vector2(340f, 56f), centered: true);

            // Message / countdown panel, bottom centre (faded in and out by IsoHud).
            RectTransform bottom = UiPanel("MessagePanel", root, skin, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(820f, 64f));
            CanvasGroup bottomGroup = bottom.gameObject.AddComponent<CanvasGroup>();
            bottomGroup.alpha = 0f;
            bottomGroup.blocksRaycasts = false;
            TextMeshProUGUI bottomText = UiText("Value", bottom, "", 30, HudTextColor, TextAlignmentOptions.Center, Vector2.zero, new Vector2(790f, 56f), centered: true);

            root.gameObject.SetActive(false);

            IsoHud hud = canvasGo.AddComponent<IsoHud>();
            SetField(hud, "grid", grid);
            SetField(hud, "skin", skin);
            SetField(hud, "resources", resources);
            SetField(hud, "waves", waveManager);
            SetField(hud, "gameManager", gameManager);
            SetField(hud, "messages", messages);
            SetField(hud, "isoRoot", root.gameObject);
            SetArray(hud, "topDownOnly", topDownLabels);
            SetArray(hud, "isoOnly", new Object[] { gameOverFrame });
            SetField(hud, "goldText", goldText);
            SetField(hud, "livesText", livesText);
            SetField(hud, "waveText", waveText);
            SetField(hud, "levelText", levelText);
            SetField(hud, "targetText", targetText);
            SetField(hud, "previousTarget", previous);
            SetField(hud, "nextTarget", next);
            SetField(hud, "bottomPanel", bottomGroup);
            SetField(hud, "bottomText", bottomText);
        }

        // The tower selector, used in both views. TowerSelector places the cards and shows the ones
        // the level needs; a card starts with the layout of a single card.
        private static void BuildTowerSelector(Transform canvas, GridManager grid, PlayerResources resources, TowerPlacer placer, SkinSet skin)
        {
            RectTransform root = UiRect("TowerSelector", canvas);
            StretchFull(root);
            root.SetAsFirstSibling(); // Under the Game Over overlay and the level select screen.

            Object[] cards = new Object[MaxTowerCards];
            for (int i = 0; i < MaxTowerCards; i++)
                cards[i] = BuildTowerCard($"TowerCard{i + 1}", root, skin);

            TowerSelector selector = root.gameObject.AddComponent<TowerSelector>();
            SetField(selector, "grid", grid);
            SetField(selector, "resources", resources);
            SetField(selector, "placer", placer);
            SetField(selector, "skin", skin);
            SetArray(selector, "cards", cards);
        }

        private static TowerCardView BuildTowerCard(string name, RectTransform parent, SkinSet skin)
        {
            RectTransform card = UiPanel(name, parent, skin, new Vector2(0f, 0f), new Vector2(20f, 16f), new Vector2(262f, 128f));
            Image panel = card.GetComponent<Image>();
            panel.raycastTarget = true; // Clickable; TowerPlacer ignores clicks over UI.
            Button button = card.gameObject.AddComponent<Button>();
            button.targetGraphic = panel;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            Outline highlight = card.gameObject.AddComponent<Outline>();
            highlight.effectColor = HudGoldColor;
            highlight.effectDistance = new Vector2(5f, -5f);
            highlight.enabled = false;

            Image icon = UiImage("TowerIcon", card, skin.towerBody, Color.white, false);
            icon.preserveAspect = true;
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 4f), new Vector2(128f, 104f));
            UiIcon("Coin", card, skin.iconCoin, new Vector2(162f, 0f), 36f);
            TextMeshProUGUI price = UiText("Price", card, "", 38, HudGoldColor, TextAlignmentOptions.Left, new Vector2(204f, 0f), new Vector2(54f, 56f));
            TextMeshProUGUI nameLabel = UiText("Name", card, "", 25, HudTextColor, TextAlignmentOptions.Left, new Vector2(146f, 26f), new Vector2(190f, 36f));
            TextMeshProUGUI counter = UiText("Counter", card, "", 28, HudTextColor, TextAlignmentOptions.Right, new Vector2(246f, -22f), new Vector2(82f, 40f));

            TowerCardView view = card.gameObject.AddComponent<TowerCardView>();
            SetField(view, "rect", card);
            SetField(view, "button", button);
            SetField(view, "icon", icon);
            SetField(view, "coin", card.Find("Coin"));
            SetField(view, "priceLabel", price);
            SetField(view, "nameLabel", nameLabel);
            SetField(view, "counterLabel", counter);
            SetField(view, "group", card.gameObject.AddComponent<CanvasGroup>());
            SetField(view, "highlight", highlight);
            return view;
        }

        private static RectTransform UiRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image UiImage(string name, Transform parent, Sprite sprite, Color color, bool sliced)
        {
            RectTransform rect = UiRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sliced)
            {
                image.type = Image.Type.Sliced;
                image.pixelsPerUnitMultiplier = UiPixelMultiplier;
            }
            return image;
        }

        // Panel anchored at 'anchor' with the same pivot (corner / edge of the screen).
        private static RectTransform UiPanel(string name, Transform parent, SkinSet skin, Vector2 anchor, Vector2 position, Vector2 size)
        {
            Image panel = UiImage(name, parent, skin.hudPanel, Color.white, true);
            SetRect(panel.rectTransform, anchor, anchor, position, size);
            return panel.rectTransform;
        }

        // Pixel icon at 'x' from the panel's left edge, vertically centred, 'height' canvas units high.
        private static void UiIcon(string name, RectTransform panel, Sprite sprite, Vector2 offset, float height)
        {
            Image icon = UiImage(name, panel, sprite, Color.white, false);
            float aspect = sprite != null ? sprite.rect.width / sprite.rect.height : 1f;
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), offset, new Vector2(height * aspect, height));
        }

        private static TextMeshProUGUI UiText(string name, RectTransform panel, string text, float size, Color color,
            TextAlignmentOptions alignment, Vector2 offset, Vector2 box, bool centered = false)
        {
            RectTransform rect = UiRect(name, panel);
            Vector2 anchor = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
            SetRect(rect, anchor, anchor, offset, box);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            return label;
        }

        // Small metal button with an arrow icon (the right arrow is the left one mirrored).
        private static Button UiArrowButton(string name, RectTransform panel, SkinSet skin, Vector2 anchor, Vector2 offset, bool right)
        {
            Image image = UiImage(name, panel, skin.hudButton, HudArrowButtonColor, true);
            image.raycastTarget = true; // Clickable; TowerPlacer ignores clicks over UI.
            SetRect(image.rectTransform, anchor, anchor, offset, new Vector2(52f, 52f));
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            Image arrow = UiImage("Arrow", image.rectTransform, skin.iconArrow, Color.white, false);
            SetRect(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(27f, 33f));
            if (right) arrow.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            return button;
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
