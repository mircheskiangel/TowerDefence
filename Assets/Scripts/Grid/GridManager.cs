using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Owns the logical cell grid of the level: size, cell <-> world conversion,
    // which cells are blocked (by towers), and the spawn/goal cells.
    // Other systems (pathfinding, tower placement) query this instead of the scene.
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [SerializeField] private int width = 16;
        [SerializeField] private int height = 10;
        [SerializeField] private float cellSize = 1f;
        [Tooltip("World position of the bottom-left corner of cell (0,0).")]
        [SerializeField] private Vector2 origin = Vector2.zero;
        [SerializeField] private Vector2Int spawnCell = new Vector2Int(0, 5);
        [SerializeField] private Vector2Int goalCell = new Vector2Int(15, 4);
        [SerializeField] private bool drawDebugGrid = true;

        private bool[,] blocked;

        // Raised whenever a cell changes between walkable and blocked.
        public event Action GridChanged;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector2 Origin => origin;
        public Vector2Int SpawnCell => spawnCell;
        public Vector2Int GoalCell => goalCell;

        // World-space center of the whole grid (used by the camera).
        public Vector3 Center => new Vector3(origin.x + width * cellSize * 0.5f, origin.y + height * cellSize * 0.5f, 0f);

        private void Awake()
        {
            // Always take over: after a scene reload the old instance may not be destroyed yet,
            // and it must never cause the new level's grid to be destroyed instead.
            Instance = this;
            blocked = new bool[width, height];
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Used by the editor LevelBuilder to set up the grid.
        public void Configure(int newWidth, int newHeight, float newCellSize, Vector2 newOrigin, Vector2Int spawn, Vector2Int goal)
        {
            width = newWidth;
            height = newHeight;
            cellSize = newCellSize;
            origin = newOrigin;
            spawnCell = spawn;
            goalCell = goal;
            blocked = new bool[width, height];
        }

        public bool IsInBounds(Vector2Int cell)
        {
            return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
        }

        public bool IsWalkable(Vector2Int cell)
        {
            return IsInBounds(cell) && !IsBlocked(cell);
        }

        public bool IsBlocked(Vector2Int cell)
        {
            EnsureArray();
            return IsInBounds(cell) && blocked[cell.x, cell.y];
        }

        public void SetBlocked(Vector2Int cell, bool value)
        {
            if (!IsInBounds(cell)) return;
            EnsureArray();
            if (blocked[cell.x, cell.y] == value) return;
            blocked[cell.x, cell.y] = value;
            GridChanged?.Invoke();
        }

        // Changes a cell WITHOUT raising GridChanged. Only for "what if" simulations
        // (e.g. TowerPlacer testing whether a tower would block the path): the caller
        // must restore the original value afterwards, so nobody else ever sees the change.
        public void SetBlockedSilently(Vector2Int cell, bool value)
        {
            if (!IsInBounds(cell)) return;
            EnsureArray();
            blocked[cell.x, cell.y] = value;
        }

        // Changes many cells at once and raises GridChanged only one time.
        public void SetBlocked(IEnumerable<Vector2Int> cells, bool value)
        {
            EnsureArray();
            bool changed = false;
            foreach (Vector2Int cell in cells)
            {
                if (!IsInBounds(cell) || blocked[cell.x, cell.y] == value) continue;
                blocked[cell.x, cell.y] = value;
                changed = true;
            }
            if (changed) GridChanged?.Invoke();
        }

        // Center of a cell in world space.
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return new Vector3(
                origin.x + (cell.x + 0.5f) * cellSize,
                origin.y + (cell.y + 0.5f) * cellSize,
                0f);
        }

        // Cell containing a world position (may be out of bounds; check IsInBounds).
        public Vector2Int WorldToCell(Vector3 world)
        {
            return new Vector2Int(
                Mathf.FloorToInt((world.x - origin.x) / cellSize),
                Mathf.FloorToInt((world.y - origin.y) / cellSize));
        }

        private void EnsureArray()
        {
            if (blocked == null || blocked.GetLength(0) != width || blocked.GetLength(1) != height)
                blocked = new bool[width, height];
        }

        // Debug view: grid lines, blocked cells, spawn and goal (Scene view, or Game view with Gizmos on).
        private void OnDrawGizmos()
        {
            if (!drawDebugGrid) return;

            Gizmos.color = new Color(1f, 1f, 1f, 0.25f);
            for (int x = 0; x <= width; x++)
            {
                Vector3 a = new Vector3(origin.x + x * cellSize, origin.y, 0f);
                Gizmos.DrawLine(a, a + Vector3.up * height * cellSize);
            }
            for (int y = 0; y <= height; y++)
            {
                Vector3 a = new Vector3(origin.x, origin.y + y * cellSize, 0f);
                Gizmos.DrawLine(a, a + Vector3.right * width * cellSize);
            }

            Vector3 size = Vector3.one * cellSize * 0.9f;
            if (blocked != null && blocked.GetLength(0) == width && blocked.GetLength(1) == height)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        if (blocked[x, y]) Gizmos.DrawCube(CellToWorld(new Vector2Int(x, y)), size);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(CellToWorld(spawnCell), size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(CellToWorld(goalCell), size);
        }
    }
}
