using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // TEMPORARY test helper for the pathfinding milestone (will be removed once towers exist).
    // Blocks hard-coded wall cells so the A* path visibly bends around them,
    // and can seal the last gap to demonstrate the "no path" case.
    // Use the keys 1 / 2 / 3 in Play mode, or the component's context menu (gear icon).
    public class PathTestObstacles : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [SerializeField] private Sprite blockSprite;
        [SerializeField] private Material blockMaterial;
        [SerializeField] private Color blockColor = new Color(0.45f, 0.45f, 0.5f);
        [SerializeField] private int sortingOrder = 2;

        // The cell that is left open at the top of the first wall.
        private static readonly Vector2Int GapCell = new Vector2Int(5, 9);

        private readonly Dictionary<Vector2Int, GameObject> blocks = new Dictionary<Vector2Int, GameObject>();

        private void Start()
        {
            if (grid == null) grid = GridManager.Instance;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) BuildWalls();
            if (keyboard.digit2Key.wasPressedThisFrame) SealGap();
            if (keyboard.digit3Key.wasPressedThisFrame) ClearWalls();
        }

        // Two walls: x=5 open at the top, x=10 open at the bottom -> the path must zig-zag.
        [ContextMenu("Test/1 - Build Walls With Gaps")]
        public void BuildWalls()
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int y = 0; y <= 8; y++) cells.Add(new Vector2Int(5, y));
            for (int y = 1; y <= 9; y++) cells.Add(new Vector2Int(10, y));
            Block(cells);
            Debug.Log("[PathTestObstacles] Walls built (gaps at (5,9) and (10,0)).");
        }

        // Closes the gap in the first wall, so no route exists any more.
        [ContextMenu("Test/2 - Seal Gap (No Path)")]
        public void SealGap()
        {
            Block(new List<Vector2Int> { GapCell });
            Debug.Log($"[PathTestObstacles] Gap {GapCell} sealed.");
        }

        [ContextMenu("Test/3 - Clear Walls")]
        public void ClearWalls()
        {
            if (grid == null) return;
            List<Vector2Int> cells = new List<Vector2Int>(blocks.Keys);
            foreach (GameObject go in blocks.Values)
                if (go != null) Destroy(go);
            blocks.Clear();
            grid.SetBlocked(cells, false);
            Debug.Log("[PathTestObstacles] Walls cleared.");
        }

        private void Block(List<Vector2Int> cells)
        {
            if (grid == null || !Application.isPlaying) return;
            foreach (Vector2Int cell in cells)
            {
                if (blocks.ContainsKey(cell)) continue;
                blocks[cell] = CreateBlockVisual(cell);
            }
            grid.SetBlocked(cells, true); // Fires GridChanged once -> PathDebugView recomputes.
        }

        private GameObject CreateBlockVisual(Vector2Int cell)
        {
            GameObject go = new GameObject($"TestBlock_{cell.x}_{cell.y}");
            go.transform.SetParent(transform, false);
            go.transform.position = grid.CellToWorld(cell);
            go.transform.localScale = Vector3.one * grid.CellSize * 0.9f;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = blockSprite;
            sr.sharedMaterial = blockMaterial;
            sr.color = blockColor;
            sr.sortingOrder = sortingOrder;
            return go;
        }
    }
}
