using UnityEngine;

namespace TowerDefense
{
    // TEMPORARY spawner for testing enemies and re-pathing (replaced by WaveManager in M5).
    // Spawns a Grunt every few seconds at the spawn cell, and a Tank on every Nth spawn.
    public class TestSpawner : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Enemy enemyPrefab;
        [SerializeField] private EnemySO gruntData;
        [SerializeField] private EnemySO tankData;
        [SerializeField] private float interval = 2f;
        [SerializeField] private int tankEvery = 5;

        private AStarPathfinder pathfinder;
        private Transform container;
        private float timer;
        private int spawnCount;

        private void Start()
        {
            if (grid == null) grid = GridManager.Instance;
            if (grid == null || enemyPrefab == null || gruntData == null || tankData == null)
            {
                Debug.LogError("[TestSpawner] Missing references - run Tower Defense/Build Level.");
                enabled = false;
                return;
            }

            pathfinder = new AStarPathfinder(grid); // One pathfinder shared by all enemies.
            container = new GameObject("Enemies").transform;
            timer = interval;
        }

        private void Update()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer += interval;
            Spawn();
        }

        private void Spawn()
        {
            spawnCount++;
            EnemySO data = spawnCount % tankEvery == 0 ? tankData : gruntData;
            Enemy enemy = Instantiate(enemyPrefab, grid.CellToWorld(grid.SpawnCell), Quaternion.identity, container);
            enemy.Initialize(data, grid, pathfinder);
        }
    }
}
