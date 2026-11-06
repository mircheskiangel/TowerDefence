using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Lets the player place towers with the mouse. Each frame it finds the grid cell under
    // the cursor, tints a highlight green (valid) or red (invalid) and shows the range circle.
    // Before a tower is placed, the placement is SIMULATED with A*: if it would cut off the
    // route from spawn to goal, or trap an enemy already walking, it is rejected ("Path blocked!").
    public class TowerPlacer : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private Camera cam;
        [SerializeField] private Tower towerPrefab;
        [SerializeField] private TowerSO towerData;
        [SerializeField] private SpriteRenderer highlight;
        [Tooltip("Isometric only: thin diamond outline drawn above the units, so the hovered cell stays visible under a tower.")]
        [SerializeField] private SpriteRenderer hoverOutline;
        [Tooltip("Isometric skin only: semi-transparent tower drawn on a cell where a tower can be placed.")]
        [SerializeField] private SpriteRenderer previewTower;
        [SerializeField] private SkinSet skin;
        [SerializeField] private Color previewColor = new Color(1f, 1f, 1f, 0.55f);
        [Tooltip("Visual only (isometric skin): material of the placement puff particles.")]
        [SerializeField] private Material puffMaterial;
        [SerializeField] private Sprite squareHighlight;
        [SerializeField] private Sprite diamondHighlight;
        [SerializeField] private SpriteRenderer rangeCircle;
        [SerializeField] private RangeIndicator rangeIndicator;
        [SerializeField] private MessageDisplay messages;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.3f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.45f);

        private Transform container;
        private AStarPathfinder pathfinder;
        private readonly Dictionary<Vector2Int, Tower> towers = new Dictionary<Vector2Int, Tower>();

        // Hover cache: the path simulation only runs when the hovered cell changes
        // (or the grid changes), not every frame.
        private Vector2Int? cachedCell;
        private bool cachedKeepsPathOpen;

        // The tower type this level lets the player buy. Called by LevelManager before Start.
        public void SetBuildTower(TowerSO tower)
        {
            towerData = tower;
        }

        // The fixed towers of the level: free, on their permanent cells (the grid has them already,
        // so GridChanged does not fire and nothing about the path changes).
        private void SpawnFixedTowers()
        {
            if (LevelManager.Instance == null) return;
            foreach (LevelData.FixedTower entry in LevelManager.Instance.Current.fixedTowers)
            {
                Tower tower = Instantiate(towerPrefab, grid.CellToWorld(entry.cell), Quaternion.identity, container);
                tower.Initialize(entry.tower, entry.cell, grid, true);
                towers[entry.cell] = tower;
            }
        }

        private void Start()
        {
            if (grid == null) grid = GridManager.Instance;
            if (resources == null) resources = PlayerResources.Instance;
            if (cam == null) cam = Camera.main;
            if (grid == null || resources == null || cam == null || towerPrefab == null || towerData == null
                || highlight == null || rangeCircle == null || messages == null)
            {
                Debug.LogError("[TowerPlacer] Missing references - run Tower Defense/Build Level.");
                enabled = false;
                return;
            }

            pathfinder = new AStarPathfinder(grid);
            container = new GameObject("Towers").transform;
            SpawnFixedTowers();
            ApplyHighlightShape();
            highlight.enabled = false;
            if (rangeIndicator == null) rangeIndicator = rangeCircle.GetComponent<RangeIndicator>();
            HideRange();
            grid.GridChanged += InvalidateHoverCache;
            grid.ProjectionChanged += ApplyHighlightShape;
        }

        private void OnDestroy()
        {
            if (grid == null) return;
            grid.GridChanged -= InvalidateHoverCache;
            grid.ProjectionChanged -= ApplyHighlightShape;
        }

        // Visual only: a square highlight in top-down, a diamond in isometric, sized to one cell.
        private void ApplyHighlightShape()
        {
            if (rangeIndicator != null) rangeIndicator.Invalidate();
            Sprite sprite = grid.ProjectionType == GridProjectionType.Isometric ? diamondHighlight : squareHighlight;
            if (sprite != null) highlight.sprite = sprite;
            Vector2 footprint = grid.CellFootprint;
            Vector2 spriteSize = highlight.sprite != null ? (Vector2)highlight.sprite.bounds.size : Vector2.one;
            highlight.transform.localScale = new Vector3(footprint.x / spriteSize.x, footprint.y / spriteSize.y, 1f);
            if (hoverOutline != null && hoverOutline.sprite != null)
            {
                Vector2 outlineSize = hoverOutline.sprite.bounds.size;
                hoverOutline.transform.localScale = new Vector3(footprint.x / outlineSize.x, footprint.y / outlineSize.y, 1f);
                hoverOutline.sortingOrder = DepthSorter.OverlayOrder;
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            bool playing = gameManager == null || gameManager.IsPlaying; // No building after Won/Lost.
            if (!playing || mouse == null || IsPointerOverUI() || !TryGetHoveredCell(mouse, out Vector2Int cell))
            {
                HideHover();
                return;
            }

            bool free = IsFreeCell(cell);
            bool affordable = resources.CanAfford(towerData.cost);
            bool keepsPathOpen = free && HoverKeepsPathOpen(cell);
            bool valid = free && affordable && keepsPathOpen;

            highlight.enabled = true;
            highlight.transform.position = grid.CellToWorld(cell);
            highlight.color = valid ? validColor : invalidColor;
            if (hoverOutline != null)
            {
                hoverOutline.enabled = grid.ProjectionType == GridProjectionType.Isometric;
                hoverOutline.transform.position = highlight.transform.position;
                Color c = highlight.color;
                hoverOutline.color = new Color(c.r, c.g, c.b, 0.9f);
            }

            ShowPreviewTower(valid ? cell : (Vector2Int?)null);

            // Range circle: around an existing tower, or preview where a new tower would go.
            if (towers.TryGetValue(cell, out Tower existing) && existing != null)
                ShowRange(cell, existing.Data.range);
            else if (valid)
                ShowRange(cell, towerData.range);
            else
                HideRange();

            if (mouse.leftButton.wasPressedThisFrame) ClickCell(cell);
        }

        // A click on a grid cell. A free, affordable cell always runs the full check again
        // (TryPlaceTower), because enemies may have moved since the hover result was cached.
        // A wall or a tower only gives a message; nothing is ever replaced. Public so the rules
        // can be tested without a real mouse.
        public void ClickCell(Vector2Int cell)
        {
            if (IsFreeCell(cell) && resources.CanAfford(towerData.cost))
                TryPlaceTower(cell);
            else if (towers.TryGetValue(cell, out Tower tower) && tower != null && tower.IsFixed)
                messages.Show("This tower is fixed");
            else if (grid.IsPermanent(cell))
                messages.Show("Cannot build on a wall");
            else if (grid.IsBlocked(cell))
                messages.Show("A tower is already here");
        }

        private bool TryGetHoveredCell(Mouse mouse, out Vector2Int cell)
        {
            return TryGetCellAtScreen(mouse.position.ReadValue(), out cell);
        }

        // Screen pixel -> world point (on the board plane) -> continuous grid position -> cell.
        // Works for any projection because the projection does the world -> grid step.
        // False if the point is outside the grid. Public so picking can be tested directly.
        public bool TryGetCellAtScreen(Vector2 screen, out Vector2Int cell)
        {
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            cell = GridManager.CellAt(grid.WorldToGrid(world));
            return grid.IsInBounds(cell);
        }

        // Basic rules: inside the grid, not already blocked, not the spawn or goal cell.
        private bool IsFreeCell(Vector2Int cell)
        {
            return grid.IsInBounds(cell)
                   && !grid.IsBlocked(cell)
                   && cell != grid.SpawnCell
                   && cell != grid.GoalCell;
        }

        private bool HoverKeepsPathOpen(Vector2Int cell)
        {
            if (cachedCell != cell)
            {
                cachedCell = cell;
                cachedKeepsPathOpen = SimulatePlacement(cell);
            }
            return cachedKeepsPathOpen;
        }

        private void InvalidateHoverCache()
        {
            cachedCell = null;
        }

        // "What if a tower stood here?" Temporarily blocks the cell WITHOUT raising GridChanged,
        // asks A* for a path from spawn to goal and from every walking enemy's current cell,
        // then restores the cell. GridChanged must not fire, otherwise every enemy would re-path
        // for a tower that does not exist (and again when it is removed), and the path view
        // would flicker. The grid is restored in 'finally', so it is always left exactly as before.
        public bool SimulatePlacement(Vector2Int cell)
        {
            if (pathfinder == null) pathfinder = new AStarPathfinder(grid);
            bool wasBlocked = grid.IsBlocked(cell);
            grid.SetBlockedSilently(cell, true);
            try
            {
                if (pathfinder.FindPath(grid.SpawnCell, grid.GoalCell) == null)
                    return false;

                foreach (Enemy enemy in EnemyRegistry.Active)
                {
                    // Enemies that are already waiting had no route before this tower, so they don't count.
                    if (enemy == null || enemy.IsDead || enemy.IsWaiting) continue;

                    // Same rule as Enemy.Repath: the start cell is always walkable, so an enemy
                    // standing on 'cell' itself is fine as long as it can walk out of it.
                    if (pathfinder.FindPath(enemy.CurrentCell, grid.GoalCell) == null)
                        return false;
                }
                return true;
            }
            finally
            {
                grid.SetBlockedSilently(cell, wasBlocked);
            }
        }

        // Places a tower if every rule allows it: game still playing, free cell, enough gold,
        // and the path stays open. Returns true on success. Used by mouse clicks (and by the
        // editor-only BalanceBot, so automated tests follow exactly the same rules).
        public bool TryPlaceTower(Vector2Int cell)
        {
            if (gameManager != null && !gameManager.IsPlaying) return false;
            if (!IsFreeCell(cell) || !resources.CanAfford(towerData.cost)) return false;
            if (!SimulatePlacement(cell))
            {
                messages.Show("Path blocked!");
                return false;
            }
            if (!resources.TrySpend(towerData.cost)) return false;

            Tower tower = Instantiate(towerPrefab, grid.CellToWorld(cell), Quaternion.identity, container);
            tower.Initialize(towerData, cell, grid);
            towers[cell] = tower;

            // The real change: raises GridChanged -> every enemy and the path view re-path.
            grid.SetBlocked(cell, true);

            // Visual only (isometric skin): a tiny dust puff around the new tower's feet.
            Sprite puff = skin != null && grid.ProjectionType == GridProjectionType.Isometric ? skin.Get(SkinSlot.Puff) : null;
            if (puff != null)
                PuffEffect.Spawn(puff, puffMaterial, grid.CellToWorld(cell),
                    DepthSorter.OrderFor(grid.SortKey(cell), cell, DepthSorter.PartBarFill), cell.x * 31 + cell.y * 17);
            return true;
        }

        public int TowerCost => towerData != null ? towerData.cost : 0;

        // Visual only: range is a circle in grid space; RangeIndicator draws it for the current
        // projection (ring sprite in top-down, projected ellipse in isometric).
        private void ShowRange(Vector2Int cell, float range)
        {
            if (rangeIndicator != null) rangeIndicator.Show(grid, cell, range);
            else
            {
                rangeCircle.enabled = true;
                rangeCircle.transform.position = grid.CellToWorld(cell);
                rangeCircle.transform.localScale = Vector3.one * (range * 2f * grid.CellSize);
            }
        }

        private void HideRange()
        {
            if (rangeIndicator != null) rangeIndicator.Hide();
            else rangeCircle.enabled = false;
        }

        // Visual only: the skin's tower body, half transparent, depth sorted like a real tower.
        private void ShowPreviewTower(Vector2Int? cell)
        {
            if (previewTower == null) return;
            Sprite sprite = cell.HasValue && skin != null && grid.ProjectionType == GridProjectionType.Isometric
                ? skin.Get(towerData.bodySlot) : null;
            previewTower.enabled = sprite != null;
            if (sprite == null) return;
            previewTower.sprite = sprite;
            previewTower.color = previewColor;
            previewTower.transform.position = grid.CellToWorld(cell.Value);
            previewTower.transform.localScale = Vector3.one * grid.CellSize;
            previewTower.sortingOrder = DepthSorter.OrderFor(grid.SortKey(cell.Value), cell.Value, DepthSorter.PartBody);
        }

        private void HideHover()
        {
            highlight.enabled = false;
            if (previewTower != null) previewTower.enabled = false;
            if (hoverOutline != null) hoverOutline.enabled = false;
            HideRange();
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
