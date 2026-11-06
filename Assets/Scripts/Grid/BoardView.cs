using UnityEngine;

namespace TowerDefense
{
    // Visual only: draws the floor (one tile per cell, checkered) and the spawn/goal markers for
    // the current projection: square tiles in top-down, diamond tiles in isometric. LevelBuilder
    // calls Rebuild() once in edit mode; at runtime it rebuilds itself when the projection is
    // switched. Tiles are sized from GridManager.CellFootprint, so no square-cell assumption.
    //
    // Isometric with a SkinSet (grass tiles filled) draws the island instead:
    // - grass tiles on the board AND on a decorative ring of 'ringWidth' cells around it
    //   (ring tiles tinted a little darker). The ring is scenery only: it is not part of the
    //   grid, so it is never clickable, walkable or blocked and A* never sees it;
    // - cliff faces under the front edges (CliffLeft under column -ring, CliffRight under row -ring);
    // - a full-screen backdrop (Background slot);
    // - decoration (trees, rocks, bushes) from the serialized list, depth sorted like units;
    // - a standing spawn portal and goal gate on the ring cells beside spawn and goal, depth
    //   sorted like units (they replace the flat markers).
    // Every slot is optional; an empty slot falls back to the M7 look for that part.
    //
    // Permanent walls are drawn in both views: boulders (Wall slot, with a shadow) depth sorted
    // like towers in isometric, small stone-coloured rounded squares in top-down.
    public class BoardView : MonoBehaviour
    {
        private const string TilesName = "Tiles";
        private const string SpawnName = "SpawnMarker";
        private const string GoalName = "GoalMarker";
        private const string CliffsName = "Cliffs";
        private const string DecorName = "Decor";
        private const string BackgroundName = "Background";
        private const string DetailsName = "GrassDetails";
        private const string WallsName = "Walls";

        [System.Serializable]
        public struct Decoration
        {
            public SkinSlot slot;          // Tree, Rock or Bush
            public int variant;
            public Vector2 gridPosition;   // On a ring cell; also its draw-order position.
        }

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

        [Header("Walls")]
        [SerializeField] private Sprite wallSquareSprite;
        [SerializeField] private Color wallSquareColor = new Color(0.62f, 0.62f, 0.66f);
        [Tooltip("Size of the top-down wall square relative to one cell.")]
        [SerializeField] private float wallSquareScale = 0.7f;
        [SerializeField] private int wallSquareOrder = 5;
        [Tooltip("The level this scene was built with: applying it again needs no rebuild.")]
        [SerializeField] private LevelData bakedLevel;

        [Header("Isometric island (skin only)")]
        [Tooltip("Cells of decorative terrain around the board (scenery, not part of the grid).")]
        [SerializeField] private int ringWidth = 1;
        [SerializeField] private Color ringTint = new Color(0.88f, 0.88f, 0.88f);
        [SerializeField] private int cliffSortingOrder = -1;
        [SerializeField] private int backgroundSortingOrder = -100;
        [Tooltip("World size the backdrop covers (centered on the board).")]
        [SerializeField] private Vector2 backgroundCover = new Vector2(40f, 26f);
        [SerializeField] private Decoration[] decorations = new Decoration[0];

        private void OnEnable()
        {
            if (grid != null) grid.ProjectionChanged += Rebuild;
        }

        private void OnDisable()
        {
            if (grid != null) grid.ProjectionChanged -= Rebuild;
        }

        // Draws the board for 'level' (the grid has it already). The scene is built with the first
        // level, so that one is used as it is.
        public void ApplyLevel(LevelData level)
        {
            if (level == bakedLevel) return;
            bakedLevel = level;
            if (skin != null)
                decorations = DecorationPlacer.Place(grid, level.decorationSeed, ringWidth,
                    skin.VariantCount(SkinSlot.Tree), skin.VariantCount(SkinSlot.Rock), skin.VariantCount(SkinSlot.Bush)).ToArray();
            Rebuild();
        }

        // Ring cell just outside the board next to 'cell' (spawn and goal lie on the board edge).
        public static Vector2Int OutsideNeighbour(GridManager grid, Vector2Int cell)
        {
            if (cell.x == 0) return cell + Vector2Int.left;
            if (cell.x == grid.Width - 1) return cell + Vector2Int.right;
            if (cell.y == 0) return cell + Vector2Int.down;
            return cell + Vector2Int.up;
        }

        public void Rebuild()
        {
            if (grid == null) return;
            foreach (string child in new[] { TilesName, SpawnName, GoalName, CliffsName, DecorName, BackgroundName, DetailsName, WallsName })
                RemoveChild(child);

            bool iso = grid.ProjectionType == GridProjectionType.Isometric;
            Sprite sprite = iso ? diamondSprite : squareSprite;
            Vector3 tileScale = ScaleFor(sprite, 1f);
            bool island = iso && skin != null && skin.Has(SkinSlot.GrassTile);
            int ring = island ? Mathf.Max(0, ringWidth) : 0;

            Transform tiles = new GameObject(TilesName).transform;
            tiles.SetParent(transform, false);
            // Island: grass tiles one order lower, so the tuft/flower overlays (at the tile order)
            // lie on the grass and the road (order 1) covers both.
            int tileOrder = island ? tileSortingOrder - 1 : tileSortingOrder;
            for (int x = -ring; x < grid.Width + ring; x++)
            {
                for (int y = -ring; y < grid.Height + ring; y++)
                {
                    Color color = (x + y) % 2 == 0 ? tileColorA : tileColorB;
                    Sprite tileSprite = sprite;
                    Vector3 scale = tileScale;
                    if (island)
                    {
                        tileSprite = skin.Get(SkinSlot.GrassTile, skin.GrassVariantFor(x, y));
                        scale = ScaleFor(tileSprite, 1f);
                        color = grid.IsInBounds(new Vector2Int(x, y)) ? Color.white : ringTint;
                    }
                    CreateSprite($"Tile_{x}_{y}", tiles, tileSprite, color, tileOrder,
                        grid.CellToWorld(new Vector2Int(x, y)), scale);
                }
            }

            if (island)
            {
                BuildGrassDetails(ring);
                BuildCliffs(ring);
                BuildBackground();
                BuildDecor();
            }

            BuildWalls(iso);

            Vector3 markerSize = ScaleFor(sprite, markerScale);
            CreateMarker(SpawnName, iso ? SkinSlot.SpawnPortal : SkinSlot.None, sprite, spawnColor, grid.SpawnCell, markerSize);
            CreateMarker(GoalName, iso ? SkinSlot.GoalGate : SkinSlot.None, sprite, goalColor, grid.GoalCell, markerSize);
        }

        // Tufts and flowers: one overlay per cell, chosen with a well-mixed hash of the cell
        // coordinates, so they do not repeat with the 4-cell period of the grass colour noise.
        private void BuildGrassDetails(int ring)
        {
            if (!skin.Has(SkinSlot.GrassDetail)) return;
            Transform details = new GameObject(DetailsName).transform;
            details.SetParent(transform, false);
            for (int x = -ring; x < grid.Width + ring; x++)
                for (int y = -ring; y < grid.Height + ring; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    Color color = grid.IsInBounds(cell) ? Color.white : ringTint;
                    CreateSprite($"Detail_{x}_{y}", details, skin.Get(SkinSlot.GrassDetail, SkinSet.MixedHash(x, y)), color,
                        tileSortingOrder, grid.CellToWorld(cell), Vector3.one * grid.CellSize);
                }
        }

        // Earth faces under the two front edges of the island: the lower-left side is the column
        // x = -ring (CliffLeft), the lower-right side is the row y = -ring (CliffRight).
        private void BuildCliffs(int ring)
        {
            Transform cliffs = new GameObject(CliffsName).transform;
            cliffs.SetParent(transform, false);
            int front = -ring;
            for (int y = front; y < grid.Height + ring; y++)
                CreateSkinned($"CliffLeft_{y}", cliffs, SkinSlot.CliffLeft, 0, cliffSortingOrder, grid.CellToWorld(new Vector2Int(front, y)));
            for (int x = front; x < grid.Width + ring; x++)
                CreateSkinned($"CliffRight_{x}", cliffs, SkinSlot.CliffRight, 0, cliffSortingOrder, grid.CellToWorld(new Vector2Int(x, front)));
        }

        private void BuildBackground()
        {
            Sprite backdrop = skin.Get(SkinSlot.Background);
            if (backdrop == null) return;
            Vector2 size = backdrop.bounds.size;
            float scale = Mathf.Max(backgroundCover.x / size.x, backgroundCover.y / size.y);
            Vector3 center = grid.WorldBounds.center;
            CreateSprite(BackgroundName, transform, backdrop, Color.white, backgroundSortingOrder,
                new Vector3(center.x, center.y, 0f), Vector3.one * scale);
        }

        // Trees, rocks and bushes on ring cells; sorted by depth like towers and enemies.
        private void BuildDecor()
        {
            Transform decor = new GameObject(DecorName).transform;
            decor.SetParent(transform, false);
            for (int i = 0; i < decorations.Length; i++)
            {
                Decoration d = decorations[i];
                Sprite s = skin.Get(d.slot, d.variant);
                if (s == null) continue;
                CreateStanding($"{d.slot}_{i}", decor, s, d.gridPosition);
            }
        }

        // A boulder with its shadow per wall cell in isometric (variant chosen from the cell's
        // coordinates), otherwise a small rounded square. Nothing is created without walls.
        private void BuildWalls(bool iso)
        {
            Transform walls = null;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int y = 0; y < grid.Height; y++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!grid.IsPermanent(cell)) continue;
                    if (walls == null)
                    {
                        walls = new GameObject(WallsName).transform;
                        walls.SetParent(transform, false);
                    }
                    Sprite boulder = iso && skin != null ? skin.Get(SkinSlot.Wall, SkinSet.MixedHash(x, y)) : null;
                    if (boulder != null) CreateBoulder(walls, cell, boulder);
                    else
                    {
                        Sprite square = iso ? diamondSprite : wallSquareSprite;
                        CreateSprite($"Wall_{x}_{y}", walls, square, wallSquareColor, wallSquareOrder,
                            grid.CellToWorld(cell), ScaleFor(square, wallSquareScale));
                    }
                }
            }
        }

        private void CreateBoulder(Transform parent, Vector2Int cell, Sprite boulder)
        {
            Vector3 position = grid.CellToWorld(cell);
            SpriteRenderer body = CreateSprite($"Wall_{cell.x}_{cell.y}", parent, boulder, Color.white, wallSquareOrder,
                position, Vector3.one * grid.CellSize);
            Sprite shadow = skin.Get(SkinSlot.WallShadow);
            DepthSorter depth = body.gameObject.AddComponent<DepthSorter>();
            if (shadow != null)
            {
                SpriteRenderer shadowRenderer = CreateSprite("Shadow", body.transform, shadow, Color.white, wallSquareOrder - 1, position, Vector3.one);
                depth.Configure(new Renderer[] { shadowRenderer, body }, new[] { DepthSorter.PartShadow, DepthSorter.PartBody });
            }
            else depth.Configure(new Renderer[] { body }, new[] { DepthSorter.PartBody });
            depth.Initialize(grid, cell);
        }

        // In isometric a filled portal/gate slot is a standing object on the ring cell beside the
        // spawn/goal, depth sorted; otherwise the plain tinted tile marker on the cell itself.
        private void CreateMarker(string objectName, SkinSlot slot, Sprite plain, Color color, Vector2Int cell, Vector3 plainScale)
        {
            Sprite standing = skin != null && slot != SkinSlot.None ? skin.Get(slot) : null;
            if (standing != null)
            {
                Sprite glow = slot == SkinSlot.SpawnPortal ? skin.Get(SkinSlot.PortalGlow) : null;
                CreateStanding(objectName, transform, standing, OutsideNeighbour(grid, cell), glow);
            }
            else
                CreateSprite(objectName, transform, plain, color, markerSortingOrder, grid.CellToWorld(cell), plainScale);
        }

        // Standing sprite (pivot at its feet, native size) with a DepthSorter at 'gridPosition'.
        // An optional glow sprite is laid over it (same pivot) and pulses softly (GlowPulse).
        private void CreateStanding(string objectName, Transform parent, Sprite sprite, Vector2 gridPosition, Sprite glow = null)
        {
            SpriteRenderer sr = CreateSprite(objectName, parent, sprite, Color.white, markerSortingOrder,
                grid.GridToWorld(gridPosition), Vector3.one * grid.CellSize);
            DepthSorter depth = sr.gameObject.AddComponent<DepthSorter>();
            if (glow != null)
            {
                SpriteRenderer glowRenderer = CreateSprite("Glow", sr.transform, glow, Color.white, markerSortingOrder,
                    sr.transform.position, Vector3.one);
                glowRenderer.gameObject.AddComponent<GlowPulse>().Configure(glowRenderer);
                depth.Configure(new Renderer[] { sr, glowRenderer }, new[] { DepthSorter.PartBody, DepthSorter.PartBarBack });
            }
            else
            {
                depth.Configure(new Renderer[] { sr }, new[] { DepthSorter.PartBody });
            }
            depth.Initialize(grid, gridPosition);
        }

        private void CreateSkinned(string objectName, Transform parent, SkinSlot slot, int variant, int order, Vector3 position)
        {
            Sprite s = skin.Get(slot, variant);
            if (s != null) CreateSprite(objectName, parent, s, Color.white, order, position, Vector3.one * grid.CellSize);
        }

        // Scale that makes 'sprite' cover one cell's footprint (times 'fraction').
        private Vector3 ScaleFor(Sprite sprite, float fraction)
        {
            Vector2 footprint = grid.CellFootprint;
            Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
            return new Vector3(footprint.x / spriteSize.x * fraction, footprint.y / spriteSize.y * fraction, fraction);
        }

        private SpriteRenderer CreateSprite(string objectName, Transform parent, Sprite sprite, Color color, int order, Vector3 position, Vector3 scale)
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
            return sr;
        }

        private void RemoveChild(string childName)
        {
            Transform child = transform.Find(childName);
            if (child == null) return;
            child.gameObject.SetActive(false); // Hidden at once (Destroy waits for the end of the frame)...
            child.SetParent(null);             // ...and detached, so a same-frame rebuild never finds it.
            if (Application.isPlaying) Destroy(child.gameObject);
            else DestroyImmediate(child.gameObject);
        }
    }
}
