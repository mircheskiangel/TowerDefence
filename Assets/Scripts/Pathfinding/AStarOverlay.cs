using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Demonstration of A*: the O key shows the cells the last spawn -> goal search expanded (its
    // closed set) as a translucent floor overlay, in both views, with the expanded count and the
    // path length in the message panel. It only reads what PathDebugView's search left behind and
    // never runs A* itself; while it is off it listens to nothing and draws nothing.
    public class AStarOverlay : MonoBehaviour
    {
        private const string LayerName = "ExpandedCells";

        [SerializeField] private GridManager grid;
        [SerializeField] private PathDebugView pathView;
        [SerializeField] private MessageDisplay messages;
        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Material material;
        [SerializeField] private Color color = new Color(0.25f, 0.75f, 1f, 0.42f);
        [Tooltip("Above the road (1), below the range indicator (3) and everything standing.")]
        [SerializeField] private int sortingOrder = 2;
        [SerializeField] private float messageSeconds = 4f;

        private readonly List<SpriteRenderer> cells = new List<SpriteRenderer>();
        private Transform layer;

        public bool Visible { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.oKey.wasPressedThisFrame) Toggle();
        }

        private void OnDestroy()
        {
            if (Visible) Unsubscribe();
        }

        public void Toggle()
        {
            Visible = !Visible;
            if (Visible)
            {
                pathView.PathChanged += Refresh;
                grid.ProjectionChanged += Draw;
                Refresh();
            }
            else
            {
                Unsubscribe();
                Hide(0);
            }
        }

        private void Unsubscribe()
        {
            if (pathView != null) pathView.PathChanged -= Refresh;
            if (grid != null) grid.ProjectionChanged -= Draw;
        }

        private void Refresh()
        {
            Draw();
            // Short: the top-down message label holds one line of about 30 characters.
            string path = pathView.CurrentPath != null ? $"path {pathView.CurrentPath.Count}" : "no path";
            messages.Show($"A*: expanded {pathView.LastExpandedCount}, {path}", messageSeconds);
        }

        private void Draw()
        {
            bool[,] closed = pathView.LastClosed;
            if (closed == null)
            {
                Hide(0);
                return;
            }

            if (layer == null)
            {
                layer = new GameObject(LayerName).transform;
                layer.SetParent(transform, false);
            }
            Sprite sprite = grid.ProjectionType == GridProjectionType.Isometric ? diamondSprite : squareSprite;
            Vector2 footprint = grid.CellFootprint;
            Vector2 size = sprite.bounds.size;
            Vector3 scale = new Vector3(footprint.x / size.x, footprint.y / size.y, 1f);

            int used = 0;
            for (int x = 0; x < closed.GetLength(0); x++)
            {
                for (int y = 0; y < closed.GetLength(1); y++)
                {
                    if (!closed[x, y]) continue;
                    SpriteRenderer sr = Cell(used++);
                    sr.sprite = sprite;
                    sr.enabled = true;
                    sr.transform.position = grid.CellToWorld(new Vector2Int(x, y));
                    sr.transform.localScale = scale;
                }
            }
            Hide(used);
        }

        private SpriteRenderer Cell(int index)
        {
            if (index < cells.Count) return cells[index];
            GameObject go = new GameObject("Expanded");
            go.transform.SetParent(layer, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = material;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            cells.Add(sr);
            return sr;
        }

        private void Hide(int fromIndex)
        {
            for (int i = fromIndex; i < cells.Count; i++) cells[i].enabled = false;
        }
    }
}
