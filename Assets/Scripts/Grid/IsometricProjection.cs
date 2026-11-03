using UnityEngine;

namespace TowerDefense
{
    // 2:1 isometric "diamond" view. Each cell is drawn as a diamond 1 tile wide and 0.5 tall
    // (times the cell size; the cell size is 1 in this project):
    //     world.x = (cx - cy) * 0.5
    //     world.y = (cx + cy) * 0.25
    // Moving +x on the grid goes right-and-up on screen, +y goes left-and-up.
    // WorldToCell is the exact inverse:  cx = x + 2y,  cy = 2y - x  (in tile units).
    public class IsometricProjection : IGridProjection
    {
        private readonly Vector2 origin;
        private readonly float tileWidth;

        public IsometricProjection(Vector2 origin, float tileWidth)
        {
            this.origin = origin;
            this.tileWidth = tileWidth;
        }

        public Vector3 CellToWorld(Vector2 cell)
        {
            return new Vector3(
                origin.x + (cell.x - cell.y) * 0.5f * tileWidth,
                origin.y + (cell.x + cell.y) * 0.25f * tileWidth,
                0f);
        }

        public Vector2 WorldToCell(Vector3 world)
        {
            float x = (world.x - origin.x) / tileWidth;
            float y = (world.y - origin.y) / tileWidth;
            return new Vector2(x + 2f * y, 2f * y - x);
        }

        // A larger cx + cy is higher on screen, i.e. further away, so it must be drawn BEHIND.
        // Negated to keep the interface rule "higher = in front" (same idea as -y in top-down).
        public float SortKey(Vector2 cell)
        {
            return -(cell.x + cell.y);
        }
    }
}
