using UnityEngine;

namespace TowerDefense
{
    // Visual only: draws the floor (one tile per cell, checkered) and the spawn/goal markers for
    // the current projection: square tiles in top-down, diamond tiles in isometric. LevelBuilder
    // calls Rebuild() once in edit mode; at runtime it rebuilds itself when the projection is
    // switched. Tiles are sized from GridManager.CellFootprint, so no square-cell assumption.
    // In isometric, filled SkinSet slots replace the tile (GrassTile variants, picked per cell)
    // and the spawn/goal markers (SpawnPortal / GoalGate); skinned sprites are drawn untinted.
    public class BoardView : MonoBehaviour
    {
        private const string TilesName = "Tiles";
        private const string SpawnName = "SpawnMarker";
        private const string GoalName = "GoalMarker";

        [SerializeField] private GridManager grid;
        [SerializeField] private Sprite squareSprite;
        [SerializeField] private Sprite diamondSprite;
        [SerializeField] private Material material;
        [SerializeField] private SkinSet skin;
        [SerializeField] private Color tileColorA = new Color(0.24f, 0.36f, 0.24f);
        [SerializeField] private Color tileColorB = new Color(0.27f, 0.40f, 0.27f);
        [SerializeField] private Color spawnColor = new Color(0.20f, 0.55f, 1.00f);
        [SerializeField] private Color goalColor = new Color(0.95f, 0.30f, 0.25f);
        [SerializeField] private int tileSortingOrder = 0;
        [SerializeField] private int markerSortingOrder = 1;
        [SerializeField] private float markerScale = 0.9f;

        private void OnEnable()
        {
            if (grid != null) grid.ProjectionChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (grid != null) grid.ProjectionChanged -= Rebuild;
        }

        public void Rebuild()
        {
            if (grid == null) return;
            RemoveChild(TilesName);
            RemoveChild(SpawnName);
            RemoveChild(GoalName);

            bool iso = grid.ProjectionType == GridProjectionType.Isometric;
            Sprite sprite = iso ? diamondSprite : squareSprite;
            Vector3 tileScale = ScaleFor(sprite, 1f);
            bool skinnedTiles = iso && skin != null && skin.Has(SkinSlot.GrassTile);

            Transform tiles = new GameObject(TilesName).transform;
            tiles.SetParent(transform, false);
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Color color = (x + y) % 2 == 0 ? tileColorA : tileColorB;
                    Sprite tileSprite = sprite;
                    Vector3 scale = tileScale;
                    if (skinnedTiles)
                    {
                        tileSprite = skin.Get(SkinSlot.GrassTile, VariantFor(x, y));
                        scale = ScaleFor(tileSprite, 1f);
                        color = Color.white;
                    }
                    CreateSprite($"Tile_{x}_{y}", tiles, tileSprite, color, tileSortingOrder,
                        grid.CellToWorld(new Vector2Int(x, y)), scale);
                }
            }

            Vector3 markerSize = ScaleFor(sprite, markerScale);
            CreateMarker(SpawnName, iso ? SkinSlot.SpawnPortal : SkinSlot.None, sprite, spawnColor, grid.SpawnCell, markerSize);
            CreateMarker(GoalName, iso ? SkinSlot.GoalGate : SkinSlot.None, sprite, goalColor, grid.GoalCell, markerSize);
        }

        // A filled skin slot is a standing object drawn at its native size (1 pixel = 1/64 unit,
        // pivot at its feet); otherwise the plain tinted tile marker.
        private void CreateMarker(string objectName, SkinSlot slot, Sprite plain, Color color, Vector2Int cell, Vector3 plainScale)
        {
            Sprite skinned = skin != null && slot != SkinSlot.None ? skin.Get(slot) : null;
            if (skinned != null)
                CreateSprite(objectName, transform, skinned, Color.white, markerSortingOrder, grid.CellToWorld(cell), Vector3.one * grid.CellSize);
            else
                CreateSprite(objectName, transform, plain, color, markerSortingOrder, grid.CellToWorld(cell), plainScale);
        }

        // Fixed per-cell variant index, so a rebuild always gives the same tile pattern.
        private static int VariantFor(int x, int y)
        {
            return ((x * 73856093) ^ (y * 19349663)) & 0x7fffffff;
        }

        // Scale that makes 'sprite' cover one cell's footprint (times 'fraction').
        private Vector3 ScaleFor(Sprite sprite, float fraction)
        {
            Vector2 footprint = grid.CellFootprint;
            Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            return new Vector3(footprint.x / spriteSize.x * fraction, footprint.y / spriteSize.y * fraction, fraction);
        }

        private void CreateSprite(string objectName, Transform parent, Sprite sprite, Color color, int order, Vector3 position, Vector3 scale)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.transform.localScale = scale;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = material;
            sr.color = color;
            sr.sortingOrder = order;
        }

        private void RemoveChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null) return;
            child.SetParent(null); // Detach first so a same-frame rebuild never finds the old one.
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
