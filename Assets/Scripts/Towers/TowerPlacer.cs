using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Lets the player place towers with the mouse. Each frame it finds the grid cell under
    // the cursor, tints a highlight green (valid) or red (invalid), and on left click
    // spends gold, spawns a tower and blocks the cell (which makes all enemies re-path).
    public class TowerPlacer : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private Camera cam;
        [SerializeField] private Tower towerPrefab;
        [SerializeField] private TowerSO towerData;
        [SerializeField] private SpriteRenderer highlight;
        [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.3f, 0.45f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.45f);

        private Transform container;

        private void Start()
        {
            if (grid == null) grid = GridManager.Instance;
            if (resources == null) resources = PlayerResources.Instance;
            if (cam == null) cam = Camera.main;
            if (grid == null || resources == null || cam == null || towerPrefab == null || towerData == null || highlight == null)
            {
                Debug.LogError("[TowerPlacer] Missing references - run Tower Defense/Build Level.");
                enabled = false;
                return;
            }

            container = new GameObject("Towers").transform;
            highlight.transform.localScale = Vector3.one * grid.CellSize;
            highlight.enabled = false;
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || IsPointerOverUI() || !TryGetHoveredCell(mouse, out Vector2Int cell))
            {
                highlight.enabled = false;
                return;
            }

            bool valid = CanPlace(cell);
            highlight.enabled = true;
            highlight.transform.position = grid.CellToWorld(cell);
            highlight.color = valid ? validColor : invalidColor;

            if (valid && mouse.leftButton.wasPressedThisFrame)
                Place(cell);
        }

        // Mouse position (pixels) -> world position -> grid cell. False if outside the grid.
        private bool TryGetHoveredCell(Mouse mouse, out Vector2Int cell)
        {
            Vector2 screen = mouse.position.ReadValue();
            Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
            cell = grid.WorldToCell(world);
            return grid.IsInBounds(cell);
        }

        // Placement rules: inside the grid, free, not spawn/goal, and affordable.
        // (Rejecting placements that block every route comes in M3 part 2.)
        public bool CanPlace(Vector2Int cell)
        {
            return grid.IsInBounds(cell)
                   && !grid.IsBlocked(cell)
                   && cell != grid.SpawnCell
                   && cell != grid.GoalCell
                   && resources.CanAfford(towerData.cost);
        }

        private void Place(Vector2Int cell)
        {
            if (!resources.TrySpend(towerData.cost)) return;

            Tower tower = Instantiate(towerPrefab, grid.CellToWorld(cell), Quaternion.identity, container);
            tower.Initialize(towerData, cell);

            // Blocking the cell raises GridChanged -> every enemy and the path view re-path.
            grid.SetBlocked(cell, true);
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
