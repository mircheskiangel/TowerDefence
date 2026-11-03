using UnityEngine;

namespace TowerDefense
{
    // Visual only: shows a tower's range. Gameplay range is a CIRCLE in grid space (radius in
    // cells). In top-down it is the original ring sprite. In isometric the circle is drawn by
    // projecting points of that grid circle through the projection, which gives the correct
    // ellipse (about 0.71 r and 0.35 r semi-axes): a faint filled mesh plus an outline.
    // The shape is only rebuilt when the cell, range or projection changes.
    public class RangeIndicator : MonoBehaviour
    {
        private const int Segments = 64;

        [SerializeField] private SpriteRenderer ringSprite;
        [SerializeField] private Material material;
        [SerializeField] private Color fillColor = new Color(0.55f, 0.85f, 1.00f, 0.12f);
        [SerializeField] private Color lineColor = new Color(0.55f, 0.85f, 1.00f, 0.9f);
        [SerializeField] private float lineWidth = 0.035f;
        [SerializeField] private int sortingOrder = 3;

        private MeshFilter fillFilter;
        private MeshRenderer fillRenderer;
        private LineRenderer outline;
        private Mesh mesh;
        private readonly Vector3[] ringPoints = new Vector3[Segments];
        private readonly Vector3[] meshVertices = new Vector3[Segments + 1];
        private readonly Color[] meshColors = new Color[Segments + 1];

        private bool hasShape;
        private Vector2Int shapeCell;
        private float shapeRange;

        public void Show(GridManager grid, Vector2Int cell, float range)
        {
            if (grid.ProjectionType != GridProjectionType.Isometric)
            {
                SetProjectedVisible(false);
                if (ringSprite == null) return;
                ringSprite.enabled = true;
                ringSprite.transform.position = grid.CellToWorld(cell);
                // The sprite is 1 world unit wide; range is in cells, so diameter = 2 * range * cell size.
                ringSprite.transform.localScale = Vector3.one * (range * 2f * grid.CellSize);
                return;
            }

            if (ringSprite != null) ringSprite.enabled = false;
            EnsureParts();
            if (!hasShape || shapeCell != cell || !Mathf.Approximately(shapeRange, range))
                BuildShape(grid, cell, range);
            SetProjectedVisible(true);
        }

        public void Hide()
        {
            if (ringSprite != null) ringSprite.enabled = false;
            SetProjectedVisible(false);
        }

        // Forget the cached shape (after a projection switch).
        public void Invalidate()
        {
            hasShape = false;
        }

        private void BuildShape(GridManager grid, Vector2Int cell, float range)
        {
            Vector3 center = grid.CellToWorld(cell);
            meshVertices[0] = center;
            meshColors[0] = fillColor;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                Vector2 gridPoint = (Vector2)cell + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * range;
                ringPoints[i] = grid.GridToWorld(gridPoint); // The grid circle, projected.
                meshVertices[i + 1] = ringPoints[i];
                meshColors[i + 1] = fillColor;
            }
            outline.positionCount = Segments;
            outline.SetPositions(ringPoints);

            if (mesh.vertexCount != meshVertices.Length)
            {
                mesh.Clear();
                mesh.vertices = meshVertices;
                int[] triangles = new int[Segments * 3];
                for (int i = 0; i < Segments; i++)
                {
                    triangles[i * 3] = 0;
                    triangles[i * 3 + 1] = 1 + (i + 1) % Segments;
                    triangles[i * 3 + 2] = 1 + i;
                }
                mesh.triangles = triangles;
            }
            else
            {
                mesh.vertices = meshVertices;
            }
            mesh.colors = meshColors;
            mesh.RecalculateBounds();

            hasShape = true;
            shapeCell = cell;
            shapeRange = range;
        }

        private void EnsureParts()
        {
            if (outline != null) return;
            Transform parent = transform.parent != null ? transform.parent : transform;

            GameObject fillGo = new GameObject("RangeFill");
            fillGo.transform.SetParent(parent, false);
            fillFilter = fillGo.AddComponent<MeshFilter>();
            fillRenderer = fillGo.AddComponent<MeshRenderer>();
            fillRenderer.sharedMaterial = material;
            fillRenderer.sortingOrder = sortingOrder;
            mesh = new Mesh { name = "RangeEllipse" };
            fillFilter.sharedMesh = mesh;

            GameObject lineGo = new GameObject("RangeOutline");
            lineGo.transform.SetParent(parent, false);
            outline = lineGo.AddComponent<LineRenderer>();
            outline.useWorldSpace = true;
            outline.loop = true;
            outline.sharedMaterial = material;
            outline.startColor = lineColor;
            outline.endColor = lineColor;
            outline.startWidth = lineWidth;
            outline.endWidth = lineWidth;
            outline.sortingOrder = sortingOrder;
        }

        private void SetProjectedVisible(bool visible)
        {
            if (fillRenderer != null) fillRenderer.enabled = visible;
            if (outline != null) outline.enabled = visible;
        }

        private void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }
    }
}
