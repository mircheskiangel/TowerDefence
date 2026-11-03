using UnityEngine;

namespace TowerDefense
{
    // Visual only: draw order for units (towers, enemies, projectiles) in the isometric view.
    // Each renderer gets   order = Band + round(SortKey * Resolution) * PartsPerStep + part,
    // where SortKey comes from the projection (-(cx + cy) in isometric: nearer the camera = higher)
    // and 'part' keeps an object's own pieces in order (shadow 0, body 1, bar back 2, bar fill 3).
    // Band keeps every unit above the floor, the hover highlight and the path line.
    // In top-down every renderer keeps its original type-based order (captured in Awake).
    //
    // Why explicit sortingOrder instead of a custom transparency sort axis: the axis is a global
    // renderer setting (it would change top-down too and cannot follow the V switch without
    // editing the renderer asset), and it sorts every sprite by its own pivot, so a health bar
    // (pivot above the head) would sort BEHIND its own enemy. Explicit orders are exact and testable.
    public class DepthSorter : MonoBehaviour
    {
        public const int Band = 12000;      // Lowest unit order on this board is about 2000: far above floor (0-1), highlight (15), path (20).
        public const int Resolution = 100;   // 0.01 cell steps of SortKey
        public const int PartsPerStep = 4;

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

        public static int OrderFor(float sortKey, int part)
        {
            return Band + Mathf.RoundToInt(sortKey * Resolution) * PartsPerStep + part;
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

            int baseOrder = OrderFor(grid.SortKey(gridPosition), 0);
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
