using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Runs the level's waves in order: countdown -> spawn the wave (coroutine) -> wait until
    // everything is spawned AND no enemy is left alive -> next countdown. After the last wave
    // it raises AllWavesCleared. Space skips a countdown. Stops at once if lives run out.
    public class WaveManager : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private List<WaveSO> waves = new List<WaveSO>();
        [SerializeField] private float firstCountdown = 10f;
        [SerializeField] private float betweenWavesCountdown = 8f;

        private AStarPathfinder pathfinder;
        private Transform container;
        private Coroutine routine;
        private bool skipRequested;

        // --- Read-only state for the UI ---
        public int CurrentWaveNumber { get; private set; }   // 1-based; 0 before the first wave.
        public int TotalWaves => waves.Count;
        public float CountdownRemaining { get; private set; }
        public bool IsCountingDown { get; private set; }
        public bool IsSpawning { get; private set; }
        public bool AllCleared { get; private set; }
        public bool Stopped { get; private set; }             // Lives ran out.

        // --- Events ---
        public event Action<int> WaveStarted;
        public event Action<int> WaveCleared;
        public event Action AllWavesCleared;

        private void OnEnable()
        {
            if (resources == null) resources = PlayerResources.Instance;
            if (resources != null) resources.LivesDepleted += HandleLivesDepleted;
        }

        private void OnDisable()
        {
            if (resources != null) resources.LivesDepleted -= HandleLivesDepleted;
        }

        private void Start()
        {
            if (grid == null) grid = GridManager.Instance;
            if (grid == null || enemyPrefab == null || waves.Count == 0)
            {
                Debug.LogError("[WaveManager] Missing references - run Tower Defense/Build Level.");
                enabled = false;
                return;
            }

            pathfinder = new AStarPathfinder(grid); // One pathfinder shared by all enemies.
            container = new GameObject("Enemies").transform;
            routine = StartCoroutine(RunWaves());
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool playing = gameManager == null || gameManager.IsPlaying;
            if (playing && IsCountingDown && keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                skipRequested = true;
        }

        private IEnumerator RunWaves()
        {
            for (int i = 0; i < waves.Count; i++)
            {
                // 1. Countdown (Space skips it).
                yield return Countdown(i == 0 ? firstCountdown : betweenWavesCountdown);

                // 2. Spawn every group of the wave.
                CurrentWaveNumber = i + 1;
                WaveStarted?.Invoke(CurrentWaveNumber);
                Debug.Log($"[WaveManager] Wave {CurrentWaveNumber}/{TotalWaves} started ({waves[i].TotalEnemies} enemies).");
                yield return SpawnWave(waves[i]);

                // 3. Wave is cleared when nothing is left to spawn AND no enemy is alive.
                //    Enemies leave EnemyRegistry the moment they die or reach the goal.
                while (EnemyRegistry.Active.Count > 0)
                    yield return null;

                WaveCleared?.Invoke(CurrentWaveNumber);
                Debug.Log($"[WaveManager] Wave {CurrentWaveNumber} cleared.");
            }

            AllCleared = true;
            routine = null;
            Debug.Log("[WaveManager] All waves cleared");
            AllWavesCleared?.Invoke();
        }

        private IEnumerator Countdown(float seconds)
        {
            skipRequested = false;
            IsCountingDown = true;
            CountdownRemaining = seconds;
            while (CountdownRemaining > 0f && !skipRequested)
            {
                yield return null;
                CountdownRemaining -= Time.deltaTime;
            }
            CountdownRemaining = 0f;
            IsCountingDown = false;
        }

        private IEnumerator SpawnWave(WaveSO wave)
        {
            IsSpawning = true;
            foreach (WaveSO.SpawnGroup group in wave.groups)
            {
                if (group.enemy == null) continue;
                if (group.delayBefore > 0f) yield return new WaitForSeconds(group.delayBefore);

                for (int n = 0; n < group.count; n++)
                {
                    SpawnEnemy(group.enemy);
                    if (n < group.count - 1) yield return new WaitForSeconds(group.spawnInterval);
                }
            }
            IsSpawning = false;
        }

        private void SpawnEnemy(EnemySO data)
        {
            Enemy enemy = Instantiate(enemyPrefab, grid.CellToWorld(grid.SpawnCell), Quaternion.identity, container);
            enemy.Initialize(data, grid, pathfinder);
        }

        // Safety stop: without it the coroutine would keep spawning (and the "wave cleared"
        // logic keep advancing) after the player has already lost. GameManager handles the
        // actual Lost state and logs it.
        private void HandleLivesDepleted()
        {
            if (routine != null) StopCoroutine(routine);
            routine = null;
            IsSpawning = false;
            IsCountingDown = false;
            Stopped = true;
        }
    }
}
