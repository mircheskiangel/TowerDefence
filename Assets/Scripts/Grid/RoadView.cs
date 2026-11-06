using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Visual only (isometric, with a skin): a dirt road drawn as overlay tiles on the cells of the
    // CURRENT spawn -> goal path, plus the ring cells leading into the portal and the gate.
    // It reads PathDebugView.CurrentPath (the same path the debug line shows) and redraws only
    // when that path changes (PathChanged, after GridChanged) or the projection is switched,
    // so it never runs A* itself. The P key only toggles the debug line, not the road.
    public class RoadView : MonoBehaviour
    {
        private const string TilesName = "RoadTiles";

        [SerializeField] private GridManager grid;
        [SerializeField] private PathDebugView pathView;
        [SerializeField] private SkinSet skin;
        [SerializeField] private Material material;
        [SerializeField] private int sortingOrder = 1;

        private void OnEnable()
        {
            if (grid != null) grid.ProjectionChanged += Rebuild;
            if (pathView != null) pathView.PathChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (grid != null) grid.ProjectionChanged -= Rebuild;
            if (pathView != null) pathView.PathChanged -= Rebuild;
        }

        private void Start()
        {
            if (pathView != null && pathView.CurrentPath != null) Rebuild(); // Path computed before we subscribed.
        }

        public void Rebuild()
        {
            Transform old = transform.Find(TilesName);
            if (old != null)
            {
                // Hidden at once: Destroy only happens at the end of the frame, and several
                // rebuilds in one frame (several towers placed at once) must not stack up.
                old.gameObject.SetActive(false);
                old.SetParent(null);
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }

            if (grid == null || skin == null || !skin.Has(SkinSlot.PathTile)) return;
            if (grid.ProjectionType != GridProjectionType.Isometric) return;

            // The ring cells in front of the portal and the gate are always road.
            var cells = new List<Vector2Int> { BoardView.OutsideNeighbour(grid, grid.SpawnCell), BoardView.OutsideNeighbour(grid, grid.GoalCell) };
            if (pathView != null && pathView.CurrentPath != null) cells.AddRange(pathView.CurrentPath);

            Transform tiles = new GameObject(TilesName).transform;
            tiles.SetParent(transform, false);
            foreach (Vector2Int cell in cells)
            {
                if (grid.IsPermanent(cell)) continue; // The road never draws on a wall.
                Sprite sprite = skin.Get(SkinSlot.PathTile, SkinSet.HashVariant(cell.x, cell.y));
                GameObject go = new GameObject($"Road_{cell.x}_{cell.y}");
                go.transform.SetParent(tiles, false);
                go.transform.position = grid.CellToWorld(cell);
                go.transform.localScale = Vector3.one * grid.CellSize;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sharedMaterial = material;
                sr.sortingOrder = sortingOrder;
            }
        }
    }
}
