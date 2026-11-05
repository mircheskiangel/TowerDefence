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
        private LineRenderer overlay; // Isometric only: dim copy above the units, so the path stays visible behind tall towers.

        public List<Vector2Int> CurrentPath { get; private set; }

        // Raised after every Recompute (the dirt road redraws from CurrentPath; no extra A*).
        public event System.Action PathChanged;

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
            line = CreateLine("PathLine", lineColor, lineWidth, sortingOrder);
            Color dim = new Color(lineColor.r, lineColor.g, lineColor.b, 0.35f);
            overlay = CreateLine("PathLineOverlay", dim, lineWidth * 0.6f, DepthSorter.OverlayOrder);
            grid.GridChanged += Recompute;
            grid.ProjectionChanged += RefreshLine;
            Recompute();
        }

        private void OnDestroy()
        {
            if (grid == null) return;
            grid.GridChanged -= Recompute;
            grid.ProjectionChanged -= RefreshLine;
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
            PathChanged?.Invoke();
        }

        private void RefreshLine()
        {
            if (line == null) return;
            bool show = visible && CurrentPath != null;
            line.enabled = show;
            if (overlay != null) overlay.enabled = show && grid.ProjectionType == GridProjectionType.Isometric;
            if (!show) return;

            line.positionCount = CurrentPath.Count;
            if (overlay != null) overlay.positionCount = CurrentPath.Count;
            for (int i = 0; i < CurrentPath.Count; i++)
            {
                Vector3 p = grid.CellToWorld(CurrentPath[i]);
                line.SetPosition(i, p);
                if (overlay != null) overlay.SetPosition(i, p);
            }
        }

        private LineRenderer CreateLine(string objectName, Color color, float width, int order)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.sharedMaterial = lineMaterial;
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.sortingOrder = order;
            lr.enabled = false;
            return lr;
        }
    }
}
