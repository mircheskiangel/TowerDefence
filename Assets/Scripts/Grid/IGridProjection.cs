using UnityEngine;

namespace TowerDefense
{
    // How logical grid coordinates are shown on screen. Gameplay (pathfinding, movement,
    // ranges, projectiles) works only in grid coordinates; a projection is the single place that
    // turns them into world positions for rendering and turns world positions (e.g. the mouse)
    // back into grid coordinates. Swapping the projection (top-down, isometric, ...) changes how
    // the game looks without changing how it plays.
    //
    // Grid coordinates are continuous: (x, y) with integer values is the CENTER of cell (x, y),
    // so cell (x, y) covers [x - 0.5, x + 0.5) x [y - 0.5, y + 0.5).
    public interface IGridProjection
    {
        // Continuous grid coordinates -> world position.
        Vector3 CellToWorld(Vector2 cell);

        // World position -> continuous grid coordinates (exact inverse of CellToWorld on the grid plane).
        Vector2 WorldToCell(Vector3 world);

        // Draw-order key for something standing at 'cell': higher = drawn in front.
        float SortKey(Vector2 cell);
    }
}
