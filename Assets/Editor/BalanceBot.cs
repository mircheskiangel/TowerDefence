using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TowerDefense.EditorTools
{
    // Editor-only balance tester (never part of a build). In Play mode it plays the level
    // automatically with simple player strategies, using the same TowerPlacer.TryPlaceTower
    // rules as a human (cost, free cell, path-blocked rejection), skipping countdowns and
    // spending only earned gold. Results go to the Console and to Logs/BalanceBot.txt.
    //
    // Usage (in Play mode):  BalanceBot.StartBatch("none,weak,reasonable,maze", 2);
    public static class BalanceBot
    {
        private enum Phase { Idle, WaitingForLevel, Running }

        private const string ResultsPath = "Logs/BalanceBot.txt";
        private const float RunTimeoutRealSeconds = 600f;

        private static Phase phase = Phase.Idle;
        private static readonly Queue<string> queue = new Queue<string>();
        private static readonly Dictionary<string, int> runCounter = new Dictionary<string, int>();
        private static float speed = 4f;
        private static bool previousRunInBackground;

        // Current run
        private static string strategy;
        private static List<Vector2Int> plan;
        private static int planIndex;
        private static int maxTowers;
        private static int towersPlaced, goldSpent, kills;
        private static float realStart;
        private static GameManager gameManager, previousGameManager;
        private static TowerPlacer placer;
        private static WaveManager waves;
        private static PlayerResources resources;

        public static bool IsRunning => phase != Phase.Idle;
        public static readonly List<string> Results = new List<string>();

        public static void StartBatch(string strategies, int repeats, float timeScale = 4f)
        {
            if (!EditorApplication.isPlaying) { Debug.LogError("[BalanceBot] Enter Play mode first."); return; }
            if (IsRunning) { Debug.LogWarning("[BalanceBot] A batch is already running."); return; }

            queue.Clear();
            runCounter.Clear();
            Results.Clear();
            foreach (string s in strategies.Split(','))
                for (int i = 0; i < repeats; i++) queue.Enqueue(s.Trim());

            speed = timeScale;
            previousRunInBackground = Application.runInBackground;
            Application.runInBackground = true; // Session only; not saved in Player Settings.
            previousGameManager = null;
            phase = Phase.WaitingForLevel;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            File.AppendAllText(ResultsPath, $"\n=== Batch {System.DateTime.Now:HH:mm:ss}: {strategies} x{repeats} at {timeScale}x ===\n");
            Debug.Log($"[BalanceBot] Batch started: {queue.Count} runs.");
        }

        public static void Stop()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Enemy.OnDied -= CountKill;
            if (phase != Phase.Idle)
            {
                Time.timeScale = 1f;
                Application.runInBackground = previousRunInBackground;
            }
            phase = Phase.Idle;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode) Stop();
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { Stop(); return; }

            if (phase == Phase.WaitingForLevel)
            {
                GameManager gm = Object.FindAnyObjectByType<GameManager>();
                if (gm != null && gm != previousGameManager && gm.IsPlaying) BeginRun(gm);
                return;
            }

            if (phase != Phase.Running) return;
            if (gameManager == null) { phase = Phase.WaitingForLevel; return; }

            if (!gameManager.IsPlaying || Time.realtimeSinceStartup - realStart > RunTimeoutRealSeconds)
            {
                EndRun();
                return;
            }

            Time.timeScale = speed;
            waves.SkipCountdown();

            // Spend gold as soon as possible, following the strategy's ordered list of cells.
            while (towersPlaced < maxTowers && planIndex < plan.Count && resources.Gold >= placer.TowerCost)
            {
                Vector2Int cell = plan[planIndex++];
                int cost = placer.TowerCost;
                if (placer.TryPlaceTower(cell))
                {
                    towersPlaced++;
                    goldSpent += cost;
                }
            }
        }

        private static void BeginRun(GameManager gm)
        {
            strategy = queue.Dequeue();
            runCounter.TryGetValue(strategy, out int n);
            runCounter[strategy] = n + 1;

            gameManager = gm;
            placer = Object.FindAnyObjectByType<TowerPlacer>();
            waves = Object.FindAnyObjectByType<WaveManager>();
            resources = PlayerResources.Instance;
            plan = BuildPlan(strategy, out maxTowers);
            planIndex = 0;
            towersPlaced = goldSpent = kills = 0;
            Enemy.OnDied += CountKill; // GameManager cleared old subscribers when this level started.
            realStart = Time.realtimeSinceStartup;
            phase = Phase.Running;
        }

        private static void CountKill(Enemy enemy) => kills++;

        private static void EndRun()
        {
            Enemy.OnDied -= CountKill;
            string result = gameManager.State == GameState.Playing ? "TIMEOUT" : gameManager.State.ToString().ToUpper();
            string line = string.Format(
                "{0,-10} #{1}  {2,-7} lives {3,2}  wave {4}/{5}  towers {6,2}  spent {7,4}  goldLeft {8,4}  kills {9,3}  gameTime {10,5:F0}s  real {11,4:F0}s",
                strategy, runCounter[strategy], result, resources.Lives, waves.CurrentWaveNumber, waves.TotalWaves,
                towersPlaced, goldSpent, resources.Gold, kills, Time.timeSinceLevelLoad, Time.realtimeSinceStartup - realStart);
            Results.Add(line);
            File.AppendAllText(ResultsPath, line + "\n");
            Debug.Log("[BalanceBot] " + line);

            previousGameManager = gameManager;
            if (queue.Count > 0)
            {
                phase = Phase.WaitingForLevel;
                gameManager.Restart();
            }
            else
            {
                File.AppendAllText(ResultsPath, "=== Batch done ===\n");
                Debug.Log("[BalanceBot] Batch done.");
                Stop();
            }
        }

        // ------------------------------------------------------------------ Strategies
        // Grid is 16x10, spawn (0,5), goal (15,4); the open-field path runs along row 5.

        private static List<Vector2Int> BuildPlan(string name, out int towerLimit)
        {
            towerLimit = int.MaxValue;
            switch (name)
            {
                case "none":
                    towerLimit = 0;
                    return new List<Vector2Int>();

                case "weak": // 4 towers in one clump next to the middle of the path.
                    towerLimit = 4;
                    return Cells((7, 6), (8, 6), (7, 7), (8, 7));

                case "reasonable": // Pairs facing the path (rows 4 and 6) from the spawn side outwards.
                {
                    var cells = new List<Vector2Int>();
                    foreach (int x in new[] { 2, 4, 6, 8, 10, 12, 3, 5, 7, 9, 11, 13 })
                    {
                        cells.Add(new Vector2Int(x, 4));
                        cells.Add(new Vector2Int(x, 6));
                    }
                    foreach (int x in new[] { 3, 5, 7, 9, 11, 13 }) // second ring if gold remains
                    {
                        cells.Add(new Vector2Int(x, 3));
                        cells.Add(new Vector2Int(x, 7));
                    }
                    return cells;
                }

                case "maze": // Walls with alternating gaps force a snake-shaped path. The first towers go
                             // right on the current path near the spawn, so enemies must wrap around them.
                {
                    var cells = new List<Vector2Int>();
                    foreach (int y in new[] { 5, 6, 4, 7, 3, 8, 2, 9 }) cells.Add(new Vector2Int(3, y));  // gap at bottom
                    foreach (int y in new[] { 1, 2, 0, 3, 4, 5, 6, 7 }) cells.Add(new Vector2Int(6, y));  // gap at top
                    foreach (int y in new[] { 8, 7, 9, 6, 5, 4, 3, 2 }) cells.Add(new Vector2Int(9, y));  // gap at bottom
                    foreach (int y in new[] { 1, 2, 0, 3, 4, 5, 6, 7 }) cells.Add(new Vector2Int(12, y)); // gap at top
                    return cells;
                }
            }
            Debug.LogError("[BalanceBot] Unknown strategy: " + name);
            towerLimit = 0;
            return new List<Vector2Int>();
        }

        private static List<Vector2Int> Cells(params (int x, int y)[] cells)
        {
            var list = new List<Vector2Int>();
            foreach (var c in cells) list.Add(new Vector2Int(c.x, c.y));
            return list;
        }
    }
}
