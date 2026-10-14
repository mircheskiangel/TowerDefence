using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Grid-based A* pathfinding on the GridManager's cells.
    // 4-directional movement, every step costs 1, Manhattan distance as the heuristic,
    // and a binary min-heap (MinHeap) as the open set.
    public class AStarPathfinder
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left
        };

        // One entry in the open set. F = G + H, where G is the cost from the start so far
        // and H is the heuristic estimate to the goal.
        private struct OpenNode
        {
            public Vector2Int Cell;
            public int G;
            public int H;
            public int F => G + H;
        }

        // Orders open nodes by lowest F; on ties prefers lower H (closer to the goal),
        // which makes A* expand fewer cells and produce straighter paths.
        private class OpenNodeComparer : IComparer<OpenNode>
        {
            public int Compare(OpenNode a, OpenNode b)
            {
                int byF = a.F.CompareTo(b.F);
                return byF != 0 ? byF : a.H.CompareTo(b.H);
            }
        }

        private readonly GridManager grid;
        private readonly MinHeap<OpenNode> open = new MinHeap<OpenNode>(new OpenNodeComparer());

        // How many cells the last search expanded (useful to show in a presentation).
        public int LastExpandedCount { get; private set; }

        public AStarPathfinder(GridManager grid)
        {
            this.grid = grid;
        }

        // Manhattan distance: the exact step count on an empty 4-directional grid,
        // so it never overestimates (admissible) and A* still returns a shortest path.
        public static int Heuristic(Vector2Int a, Vector2Int b)
        {
            return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        }

        // Returns the cells from start to goal (both inclusive), or null if no path exists.
        // The start cell is ALWAYS treated as walkable, even if blocked (e.g. an enemy standing
        // where a tower was just built): it is never checked, only its neighbours are.
        public List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
        {
            LastExpandedCount = 0;
            if (grid == null || !grid.IsInBounds(start) || !grid.IsWalkable(goal)) return null;

            int w = grid.Width;
            int h = grid.Height;
            int[,] gCost = new int[w, h];
            bool[,] closed = new bool[w, h];
            Vector2Int[,] cameFrom = new Vector2Int[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    gCost[x, y] = int.MaxValue;

            open.Clear();
            gCost[start.x, start.y] = 0;
            open.Push(new OpenNode { Cell = start, G = 0, H = Heuristic(start, goal) });

            while (open.Count > 0)
            {
                OpenNode current = open.Pop();
                Vector2Int cell = current.Cell;

                // A cell can be pushed more than once (when a cheaper route is found later);
                // older, more expensive copies are simply skipped here ("lazy deletion").
                if (closed[cell.x, cell.y]) continue;
                closed[cell.x, cell.y] = true;
                LastExpandedCount++;

                if (cell == goal) return BuildPath(cameFrom, start, goal);

                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int next = cell + dir;
                    if (!grid.IsWalkable(next) || closed[next.x, next.y]) continue;

                    int newG = current.G + 1;
                    if (newG >= gCost[next.x, next.y]) continue;

                    gCost[next.x, next.y] = newG;
                    cameFrom[next.x, next.y] = cell;
                    open.Push(new OpenNode { Cell = next, G = newG, H = Heuristic(next, goal) });
                }
            }

            return null; // Open set exhausted: the goal is unreachable.
        }

        // Walks the cameFrom links backwards from the goal, then reverses the list.
        private static List<Vector2Int> BuildPath(Vector2Int[,] cameFrom, Vector2Int start, Vector2Int goal)
        {
            List<Vector2Int> path = new List<Vector2Int>();
            Vector2Int cell = goal;
            while (cell != start)
            {
                path.Add(cell);
                cell = cameFrom[cell.x, cell.y];
            }
            path.Add(start);
            path.Reverse();
            return path;
        }
    }
}
