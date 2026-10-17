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
        [SerializeField] private SpriteRenderer rangeCircle;
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
            highlight.transform.localScale = Vector3.one * grid.CellSize;
            highlight.enabled = false;
            rangeCircle.enabled = false;
            grid.GridChanged += InvalidateHoverCache;
        }

        private void OnDestroy()
        {
            if (grid != null) grid.GridChanged -= InvalidateHoverCache;
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

            // Range circle: around an existing tower, or preview where a new tower would go.
            if (towers.TryGetValue(cell, out Tower existing) && existing != null)
                ShowRange(cell, existing.Data.range);
            else if (valid)
                ShowRange(cell, towerData.range);
            else
                rangeCircle.enabled = false;

            // A click on a free, affordable cell always runs the full check again (TryPlaceTower),
            // because enemies may have moved since the hover result was cached.
            if (mouse.leftButton.wasPressedThisFrame && free && affordable)
                TryPlaceTower(cell);
        }

        // Mouse position (pixels) -> world position -> grid cell. False if outside the grid.
        private bool TryGetHoveredCell(Mouse mouse, out Vector2Int cell)
        {
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            cell = grid.WorldToCell(world);
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
            tower.Initialize(towerData, cell);
            towers[cell] = tower;

            // The real change: raises GridChanged -> every enemy and the path view re-path.
            grid.SetBlocked(cell, true);
            return true;
        }

        public int TowerCost => towerData != null ? towerData.cost : 0;

        private void ShowRange(Vector2Int cell, float range)
        {
            rangeCircle.enabled = true;
            rangeCircle.transform.position = grid.CellToWorld(cell);
            rangeCircle.transform.localScale = Vector3.one * range * 2f; // Sprite is 1 unit wide: diameter = 2 * range.
        }

        private void HideHover()
        {
            highlight.enabled = false;
            rangeCircle.enabled = false;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
