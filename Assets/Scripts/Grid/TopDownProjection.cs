using UnityEngine;

namespace TowerDefense
{
    // The original straight top-down view: grid x -> world x, grid y -> world y, each cell is a
    // square of 'cellSize' world units, and 'origin' is the world position of the bottom-left
    // corner of cell (0,0). This reproduces exactly the conversions the game used before.
    public class TopDownProjection : IGridProjection
    {
        private readonly Vector2 origin;
        private readonly float cellSize;

        public TopDownProjection(Vector2 origin, float cellSize)
        {
            this.origin = origin;
            this.cellSize = cellSize;
        }

        public Vector3 CellToWorld(Vector2 cell)
        {
            return new Vector3(
                origin.x + (cell.x + 0.5f) * cellSize,
                origin.y + (cell.y + 0.5f) * cellSize,
                0f);
        }

        public Vector2 WorldToCell(Vector3 world)
        {
            return new Vector2(
                (world.x - origin.x) / cellSize - 0.5f,
                (world.y - origin.y) / cellSize - 0.5f);
        }

        // In a flat top-down view nothing overlaps by depth, so draw order does not depend on the
        // position. Lower rows get a slightly higher key, which is the natural order once objects
        // get height (things nearer the bottom of the screen are in front). Not used yet.
        public float SortKey(Vector2 cell)
        {
            return -cell.y;
        }
    }
}
