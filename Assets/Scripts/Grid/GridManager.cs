using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Owns the logical cell grid of the level: size, which cells are blocked (by towers) or
    // permanent walls, the spawn/goal cells, and the ONLY projection between grid and world
    // coordinates.
    // Gameplay works in grid coordinates; every grid <-> world conversion goes through here.
    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }

        [SerializeField] private int width = 16;
        [SerializeField] private int height = 10;
        [Tooltip("World size of one cell (used by the projection and to size sprites).")]
        [SerializeField] private float cellSize = 1f;
        [Tooltip("World position of the bottom-left corner of cell (0,0).")]
        [SerializeField] private Vector2 origin = Vector2.zero;
        [SerializeField] private Vector2Int spawnCell = new Vector2Int(0, 5);
        [SerializeField] private Vector2Int goalCell = new Vector2Int(15, 4);
        [Tooltip("How the board is drawn. Gameplay is identical in every projection.")]
        [SerializeField] private GridProjectionType projectionType = GridProjectionType.TopDown;
        [SerializeField] private bool drawDebugGrid = true;

        private bool[,] blocked;
        private bool[,] permanent; // Walls of the level: stay blocked whatever happens to 'blocked'.
        private IGridProjection projection;

        // Raised whenever a cell changes between walkable and blocked.
        public event Action GridChanged;

        // Raised when the projection is switched at runtime (visuals must be re-placed).
        public event Action ProjectionChanged;

        public int Width => width;
        public int Height => height;
        public float CellSize => cellSize;
        public Vector2Int SpawnCell => spawnCell;
        public Vector2Int GoalCell => goalCell;
        public GridProjectionType ProjectionType => projectionType;

        // Created on first use, so it also works in edit mode (LevelBuilder) where Awake does not run.
        public IGridProjection Projection => projection ??= CreateProjection(projectionType);

        private IGridProjection CreateProjection(GridProjectionType type)
        {
            switch (type)
            {
                case GridProjectionType.Isometric: return new IsometricProjection(origin, cellSize);
                default: return new TopDownProjection(origin, cellSize);
            }
        }

        // Switches how the board is drawn. Only visuals react (ProjectionChanged); grid state,
        // paths and all gameplay stay exactly the same because they live in grid coordinates.
        public void SetProjection(GridProjectionType type)
        {
            if (type == projectionType && projection != null) return;
            projectionType = type;
            projection = null;
            ProjectionChanged?.Invoke();
        }

        private void Awake()
        {
            // Always take over: after a scene reload the old instance may not be destroyed yet,
            // and it must never cause the new level's grid to be destroyed instead.
            Instance = this;
            blocked = new bool[width, height];
            projection = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            projection = null; // Origin or cell size changed in the Inspector.
        }

        // Used by the editor LevelBuilder to set up the grid.
        public void Configure(int newWidth, int newHeight, float newCellSize, Vector2 newOrigin, Vector2Int spawn, Vector2Int goal,
            GridProjectionType type = GridProjectionType.TopDown)
        {
            projectionType = type;
            width = newWidth;
            height = newHeight;
            cellSize = newCellSize;
            origin = newOrigin;
            spawnCell = spawn;
            goalCell = goal;
            blocked = new bool[width, height];
            projection = null;
        }

        // Takes size, spawn, goal and walls from the level. Called by LevelManager before any other
        // script's Awake. Keeps the projection, cell size and origin.
        public void ApplyLevel(LevelData level)
        {
            width = level.width;
            height = level.height;
            spawnCell = level.spawnCell;
            goalCell = level.goalCell;
            blocked = new bool[width, height];
            permanent = new bool[width, height];
            foreach (Vector2Int cell in level.wallCells)
                if (IsInBounds(cell)) permanent[cell.x, cell.y] = true;
            projection = null;
        }

        // ------------------------------------------------------------- Cell state

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
            return IsInBounds(cell) && (blocked[cell.x, cell.y] || IsPermanent(cell));
        }

        // A wall of the level: blocked for A*, cannot be built on, sold or replaced.
        public bool IsPermanent(Vector2Int cell)
        {
            return permanent != null && IsInBounds(cell) && permanent[cell.x, cell.y];
        }

        public void SetBlocked(Vector2Int cell, bool value)
        {
            if (!IsInBounds(cell) || IsPermanent(cell)) return;
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
            if (!IsInBounds(cell) || IsPermanent(cell)) return;
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
                if (!IsInBounds(cell) || IsPermanent(cell) || blocked[cell.x, cell.y] == value) continue;
                blocked[cell.x, cell.y] = value;
                changed = true;
            }
            if (changed) GridChanged?.Invoke();
        }

        // ------------------------------------------------------------- Conversions
        // Grid positions are continuous: integer values are cell centers (see IGridProjection).

        // World position of a cell's center.
        public Vector3 CellToWorld(Vector2Int cell)
        {
            return Projection.CellToWorld(cell);
        }

        // World position of a continuous grid position (e.g. an enemy between two cells).
        public Vector3 GridToWorld(Vector2 gridPosition)
        {
            return Projection.CellToWorld(gridPosition);
        }

        // Continuous grid position of a world point.
        public Vector2 WorldToGrid(Vector3 world)
        {
            return Projection.WorldToCell(world);
        }

        // Cell containing a world position (may be out of bounds; check IsInBounds).
        public Vector2Int WorldToCell(Vector3 world)
        {
            return CellAt(WorldToGrid(world));
        }

        // Cell containing a continuous grid position: cell (x, y) covers [x - 0.5, x + 0.5).
        public static Vector2Int CellAt(Vector2 gridPosition)
        {
            return new Vector2Int(
                Mathf.FloorToInt(gridPosition.x + 0.5f),
                Mathf.FloorToInt(gridPosition.y + 0.5f));
        }

        // Draw-order key for something standing at a grid position (higher = in front).
        public float SortKey(Vector2 gridPosition)
        {
            return Projection.SortKey(gridPosition);
        }

        // World-space bounding box of one cell's footprint (a square in top-down, a diamond's
        // box in isometric). Used to size tile, marker and highlight sprites for any projection.
        public Vector2 CellFootprint
        {
            get
            {
                Bounds b = new Bounds(GridToWorld(new Vector2(-0.5f, -0.5f)), Vector3.zero);
                b.Encapsulate(GridToWorld(new Vector2(0.5f, -0.5f)));
                b.Encapsulate(GridToWorld(new Vector2(-0.5f, 0.5f)));
                b.Encapsulate(GridToWorld(new Vector2(0.5f, 0.5f)));
                return b.size;
            }
        }

        // World-space bounding box of the whole grid (used by the camera). Built from the four
        // outer CORNERS of the board (cell edges, not centers), so it includes the half-cell /
        // half-diamond margin around the edge cells in every projection.
        public Bounds WorldBounds
        {
            get
            {
                Bounds bounds = new Bounds(GridToWorld(new Vector2(-0.5f, -0.5f)), Vector3.zero);
                bounds.Encapsulate(GridToWorld(new Vector2(width - 0.5f, -0.5f)));
                bounds.Encapsulate(GridToWorld(new Vector2(-0.5f, height - 0.5f)));
                bounds.Encapsulate(GridToWorld(new Vector2(width - 0.5f, height - 0.5f)));
                return bounds;
            }
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
                Gizmos.DrawLine(GridToWorld(new Vector2(x - 0.5f, -0.5f)), GridToWorld(new Vector2(x - 0.5f, height - 0.5f)));
            for (int y = 0; y <= height; y++)
                Gizmos.DrawLine(GridToWorld(new Vector2(-0.5f, y - 0.5f)), GridToWorld(new Vector2(width - 0.5f, y - 0.5f)));

            if (projectionType == GridProjectionType.Isometric)
            {
                // Cell footprints are diamonds here: outline them through the projection.
                if (blocked != null && blocked.GetLength(0) == width && blocked.GetLength(1) == height)
                {
                    Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                            if (blocked[x, y]) DrawCellOutline(new Vector2Int(x, y), 0.45f);
                    Gizmos.color = new Color(0.75f, 0.75f, 0.8f, 0.9f);
                    for (int x = 0; x < width; x++)
                        for (int y = 0; y < height; y++)
                            if (IsPermanent(new Vector2Int(x, y))) DrawCellOutline(new Vector2Int(x, y), 0.35f);
                }
                Gizmos.color = Color.green;
                DrawCellOutline(spawnCell, 0.45f);
                Gizmos.color = Color.red;
                DrawCellOutline(goalCell, 0.45f);
                return;
            }

            Vector3 size = Vector3.one * cellSize * 0.9f;
            if (blocked != null && blocked.GetLength(0) == width && blocked.GetLength(1) == height)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.35f);
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        if (blocked[x, y]) Gizmos.DrawCube(CellToWorld(new Vector2Int(x, y)), size);
                Gizmos.color = new Color(0.75f, 0.75f, 0.8f, 0.6f);
                for (int x = 0; x < width; x++)
                    for (int y = 0; y < height; y++)
                        if (IsPermanent(new Vector2Int(x, y))) Gizmos.DrawCube(CellToWorld(new Vector2Int(x, y)), size * 0.75f);
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(CellToWorld(spawnCell), size);
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(CellToWorld(goalCell), size);
        }

        // Gizmo helper: outline of a cell's footprint, 'half' cells from its center on each side.
        private void DrawCellOutline(Vector2Int cell, float half)
        {
            Vector3 a = GridToWorld(cell + new Vector2(-half, -half));
            Vector3 b = GridToWorld(cell + new Vector2(half, -half));
            Vector3 c = GridToWorld(cell + new Vector2(half, half));
            Vector3 d = GridToWorld(cell + new Vector2(-half, half));
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }
    }
}
