using UnityEngine;

namespace TowerDefense
{
    // Visual only: draw order for units (towers, enemies, projectiles) in the isometric view.
    //
    //   order = Band + (round(SortKey * Resolution) * TieSlots + tieSlot) * PartsPerStep + part
    //
    // - SortKey comes from the projection (-(cx + cy) in isometric): nearer the camera = higher.
    // - tieSlot breaks ties between units at the same depth (side by side on screen): it grows
    //   with the grid x coordinate cx, so equal-depth units always get different, fixed orders
    //   and can never swap from frame to frame.
    // - part keeps an object's own pieces in order (shadow 0, base/body 1-2, bar back 2, bar fill 3).
    // - Band lifts every unit above the floor, the hover highlight and the floor path line;
    //   OverlayOrder is above every unit (path copy and hover outline drawn "through" units).
    // In top-down every renderer keeps its original type-based order (captured in Awake).
    //
    // Why explicit sortingOrder instead of a custom transparency sort axis: the axis is a global
    // renderer setting (it would change top-down too and cannot follow the V switch without
    // editing the renderer asset), and it sorts every sprite by its own pivot, so a health bar
    // (pivot above the head) would sort BEHIND its own enemy. Explicit orders are exact and testable.
    public class DepthSorter : MonoBehaviour
    {
        public const int Band = 31000;
        public const int Resolution = 10;    // depth steps of 0.1 cell
        public const int TieSlots = 32;      // cx from -0.5 to 15.5 in steps of 0.5
        public const int PartsPerStep = 4;
        public const int MinOrder = 100;     // never below this (floor 0-1, range 3, highlight 15, path 20)
        public const int MaxOrder = 32600;   // never above this (overlays use OverlayOrder)
        public const int OverlayOrder = 32700;

        public const int PartShadow = 0;
        public const int PartBody = 1;
        public const int PartBarBack = 2;
        public const int PartBarFill = 3;

        [SerializeField] private Renderer[] renderers = new Renderer[0];
        [SerializeField] private int[] parts = new int[0];

        private int[] typeOrders;
        private GridManager grid;
        private Vector2 gridPosition;
        private bool sortedByDepth;
        private int lastBase = int.MinValue;

        // Order of 'part' for an object at continuous grid position 'gridPosition' with depth 'sortKey'.
        public static int OrderFor(float sortKey, Vector2 gridPosition, int part)
        {
            int depthStep = Mathf.RoundToInt(sortKey * Resolution);
            int tieSlot = Mathf.Clamp(Mathf.FloorToInt((gridPosition.x + 0.5f) * 2f), 0, TieSlots - 1);
            int order = Band + (depthStep * TieSlots + tieSlot) * PartsPerStep + part;
            return Mathf.Clamp(order, MinOrder, MaxOrder);
        }

        private void Awake()
        {
            CaptureTypeOrders();
        }

        public void Initialize(GridManager gridManager, Vector2 position)
        {
            grid = gridManager;
            gridPosition = position;
            Apply(true);
        }

        // Call whenever the object moves (enemies and projectiles: every frame).
        public void SetGridPosition(Vector2 position)
        {
            gridPosition = position;
            Apply(false);
        }

        // Call after a projection switch.
        public void Refresh()
        {
            Apply(true);
        }

        private void Apply(bool force)
        {
            if (grid == null) return;
            if (typeOrders == null) CaptureTypeOrders();

            if (grid.ProjectionType != GridProjectionType.Isometric)
            {
                if (sortedByDepth || force)
                {
                    for (int i = 0; i < renderers.Length; i++)
                        if (renderers[i] != null) renderers[i].sortingOrder = typeOrders[i];
                    sortedByDepth = false;
                    lastBase = int.MinValue;
                }
                return;
            }

            int baseOrder = OrderFor(grid.SortKey(gridPosition), gridPosition, 0);
            if (!force && sortedByDepth && baseOrder == lastBase) return; // unchanged: no work
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sortingOrder = Mathf.Min(baseOrder + (i < parts.Length ? parts[i] : PartBody), MaxOrder);
            lastBase = baseOrder;
            sortedByDepth = true;
        }

        private void CaptureTypeOrders()
        {
            typeOrders = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                typeOrders[i] = renderers[i] != null ? renderers[i].sortingOrder : 0;
        }
    }
}
