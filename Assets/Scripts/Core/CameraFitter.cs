using UnityEngine;

namespace TowerDefense
{
    // Keeps the orthographic camera centered on the grid and sized so the whole
    // grid (plus a margin) is visible at any window aspect ratio.
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [Tooltip("Extra world units shown around the grid.")]
        [SerializeField] private float margin = 1f;

        private Camera cam;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
        }

        private void LateUpdate()
        {
            Fit();
        }

        public void SetGrid(GridManager target)
        {
            grid = target;
        }

        public void Fit()
        {
            if (grid == null) return;
            if (cam == null) cam = GetComponent<Camera>();

            float gridWidth = grid.Width * grid.CellSize + margin * 2f;
            float gridHeight = grid.Height * grid.CellSize + margin * 2f;

            // Orthographic size is half the visible height; widen it if the window is too narrow.
            float sizeForHeight = gridHeight * 0.5f;
            float sizeForWidth = gridWidth * 0.5f / Mathf.Max(cam.aspect, 0.01f);
            cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);

            Vector3 center = grid.Center;
            transform.position = new Vector3(center.x, center.y, -10f);
        }
    }
}
