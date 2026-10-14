using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Debug visualisation of A*: draws the current spawn -> goal path with a LineRenderer.
    // Press P to toggle it. The path is recomputed whenever the grid changes.
    public class PathDebugView : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineColor = new Color(1f, 0.85f, 0.1f);
        [SerializeField] private float lineWidth = 0.12f;
        [SerializeField] private int sortingOrder = 20;
        [SerializeField] private bool visible = true;

        private AStarPathfinder pathfinder;
        private LineRenderer line;

        public List<Vector2Int> CurrentPath { get; private set; }

        private void Start()
        {
            if (grid == null) grid = GridManager.Instance;
            if (grid == null)
            {
                Debug.LogError("[PathDebugView] No GridManager found.");
                enabled = false;
                return;
            }

            pathfinder = new AStarPathfinder(grid);
            line = CreateLine();
            grid.GridChanged += Recompute;
            Recompute();
        }

        private void OnDestroy()
        {
            if (grid != null) grid.GridChanged -= Recompute;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.pKey.wasPressedThisFrame)
            {
                visible = !visible;
                RefreshLine();
            }
        }

        public void Recompute()
        {
            CurrentPath = pathfinder.FindPath(grid.SpawnCell, grid.GoalCell);

            if (CurrentPath == null)
                Debug.LogWarning($"[PathDebugView] No path from spawn {grid.SpawnCell} to goal {grid.GoalCell}! " +
                                 $"(A* expanded {pathfinder.LastExpandedCount} cells)");
            else
                Debug.Log($"[PathDebugView] Path found: {CurrentPath.Count} cells, " +
                          $"A* expanded {pathfinder.LastExpandedCount} cells.");

            RefreshLine();
        }

        private void RefreshLine()
        {
            if (line == null) return;
            bool show = visible && CurrentPath != null;
            line.enabled = show;
            if (!show) return;

            line.positionCount = CurrentPath.Count;
            for (int i = 0; i < CurrentPath.Count; i++)
                line.SetPosition(i, grid.CellToWorld(CurrentPath[i]));
        }

        private LineRenderer CreateLine()
        {
            GameObject go = new GameObject("PathLine");
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = lineMaterial;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sortingOrder = sortingOrder;
            return lr;
        }
    }
}
