using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Trees, rocks and bushes on the decorative ring, placed with a fixed seed, from the level's
    // size, spawn and goal. Never on the portal/gate cells or the ring cells next to them; the two
    // front edges (lower-left column x = -1 and lower-right row y = -1) get only low rocks and
    // bushes, so nothing hides the front row of the board.
    public static class DecorationPlacer
    {
        public static List<BoardView.Decoration> Place(GridManager grid, int seed, int ringWidth, int treeVariants, int rockVariants, int bushVariants)
        {
            var list = new List<BoardView.Decoration>();
            System.Random rng = new System.Random(seed);
            Vector2Int portal = BoardView.OutsideNeighbour(grid, grid.SpawnCell);
            Vector2Int gate = BoardView.OutsideNeighbour(grid, grid.GoalCell);
            for (int x = -ringWidth; x < grid.Width + ringWidth; x++)
            {
                for (int y = -ringWidth; y < grid.Height + ringWidth; y++)
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
                    int variants = slot == SkinSlot.Tree ? treeVariants : slot == SkinSlot.Rock ? rockVariants : bushVariants;
                    list.Add(new BoardView.Decoration { slot = slot, variant = rng.Next(variants), gridPosition = cell + offset });
                }
            }
            return list;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
}
