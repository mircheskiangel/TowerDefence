using UnityEngine;

namespace TowerDefense
{
    // Visual only: draw order for standing things (towers, enemies, projectiles, decoration,
    // portal and gate) in the isometric view.
    //
    //   order = MinOrder + (depthIndex * TieSlots + tieSlot) * PartsPerStep + part
    //   depthIndex = round(SortKey * Resolution) + round(MaxDepth * Resolution)
    //   tieSlot    = floor(cx - MinColumn)          (one slot per cell column)
    //
    // - SortKey comes from the projection (-(cx + cy) in isometric): nearer the camera = higher.
    //   Depth steps are 0.1 cell. The formula covers cx + cy from MinDepth to MaxDepth, i.e. the
    //   board plus the decorative ring (cx -1..16, cy -1..10) with half a cell to spare, so no
    //   object in the level is ever clamped.
    // - tieSlot breaks ties between objects at the same depth (side by side on screen): the one
    //   in the higher cell column (larger cx) is drawn in front, so equal-depth objects always get
    //   different, fixed orders and can never swap from frame to frame.
    // - part keeps an object's own pieces in order (shadow 0, base/body 1-2, bar back 2, bar fill 3).
    // - Every unit order lies in MinOrder..MaxOrder: above the floor, road, range indicator,
    //   hover highlight and floor path line (orders 0-20), below OverlayOrder (path copy and
    //   hover outline drawn "through" units).
    // In top-down every renderer keeps its original type-based order (captured in Awake).
    //
    // Why explicit sortingOrder instead of a custom transparency sort axis: the axis is a global
    // renderer setting (it would change top-down too and cannot follow the V switch without
    // editing the renderer asset), and it sorts every sprite by its own pivot, so a health bar
    // (pivot above the head) would sort BEHIND its own enemy. Explicit orders are exact and testable.
    // Why not sorting layers: top-down relies on orders interleaved across "floor" and "units"
    // (path line 20 over towers 5, highlight 15 over enemies 10), so every renderer would have to
    // switch layers per projection. One formula here is the smaller change.
    public class DepthSorter : MonoBehaviour
    {
        public const int Resolution = 10;      // depth steps of 0.1 cell
        public const float MinDepth = -2.5f;   // smallest cx + cy covered (ring corner -1,-1 minus margin)
        public const float MaxDepth = 26.5f;   // largest cx + cy covered (ring corner 16,10 plus margin)
        public const float MinColumn = -1.5f;  // tie slot 0 starts here (ring column -1 minus margin)
        public const int TieSlots = 20;        // cell columns -1.5 .. 18.5
        public const int PartsPerStep = 4;
        public const int DepthSteps = (int)((MaxDepth - MinDepth) * Resolution) + 1; // 291
        public const int MinOrder = 100;       // never below this (floor 0-1, road 1, range 3, highlight 15, path 20)
        public const int MaxOrder = MinOrder + DepthSteps * TieSlots * PartsPerStep - 1; // 23379
        public const int OverlayOrder = 32700; // above every unit

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

        // Order of 'part' for an object at continuous grid position 'gridPosition' with depth
        // 'sortKey'. Not clamped: inside the covered range it is always within MinOrder..MaxOrder
        // (IsCovered tells whether a position is inside that range).
        public static int OrderFor(float sortKey, Vector2 gridPosition, int part)
        {
            int depthIndex = Mathf.RoundToInt(sortKey * Resolution) + Mathf.RoundToInt(MaxDepth * Resolution);
            int tieSlot = Mathf.FloorToInt(gridPosition.x - MinColumn);
            return MinOrder + (depthIndex * TieSlots + tieSlot) * PartsPerStep + part;
        }

        // True when the formula needs no clamping for this position (sortKey = -(cx + cy)).
        public static bool IsCovered(float sortKey, Vector2 gridPosition)
        {
            int depthIndex = Mathf.RoundToInt(sortKey * Resolution) + Mathf.RoundToInt(MaxDepth * Resolution);
            int tieSlot = Mathf.FloorToInt(gridPosition.x - MinColumn);
            return depthIndex >= 0 && depthIndex < DepthSteps && tieSlot >= 0 && tieSlot < TieSlots;
        }

        private void Awake()
        {
            CaptureTypeOrders();
        }

        // For objects created in code (decoration, portal, gate): which renderers, which parts.
        // Their current orders become the top-down (type) orders.
        public void Configure(Renderer[] newRenderers, int[] newParts)
        {
            renderers = newRenderers;
            parts = newParts;
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

            // Safety net only: nothing in the level lies outside the covered range.
            int baseOrder = Mathf.Clamp(OrderFor(grid.SortKey(gridPosition), gridPosition, 0), MinOrder, MaxOrder - PartsPerStep + 1);
            if (!force && sortedByDepth && baseOrder == lastBase) return; // unchanged: no work
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].sortingOrder = baseOrder + (i < parts.Length ? parts[i] : PartBody);
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
