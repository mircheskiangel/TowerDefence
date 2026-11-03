using UnityEngine;

namespace TowerDefense
{
    // Keeps the orthographic camera centered on the grid. With autoFit (default) it sizes itself
    // so the whole projected grid (grid.WorldBounds plus a margin) is visible at any aspect
    // ratio (16:9, 16:10, ...); otherwise it uses the configured orthographic size.
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] private GridManager grid;
        [Tooltip("Extra world units shown around the grid.")]
        [SerializeField] private float margin = 1f;
        [Tooltip("On: size the camera to fit the whole grid. Off: use Orthographic Size below.")]
        [SerializeField] private bool autoFit = true;
        [SerializeField] private float orthographicSize = 6f;

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

            Bounds bounds = grid.WorldBounds; // The grid as the projection draws it.
            float gridWidth = bounds.size.x + margin * 2f;
            float gridHeight = bounds.size.y + margin * 2f;

            // Orthographic size is half the visible height; widen it if the window is too narrow.
            float sizeForHeight = gridHeight * 0.5f;
            float sizeForWidth = gridWidth * 0.5f / Mathf.Max(cam.aspect, 0.01f);
            cam.orthographicSize = autoFit ? Mathf.Max(sizeForHeight, sizeForWidth) : orthographicSize;

            Vector3 center = bounds.center;
            transform.position = new Vector3(center.x, center.y, -10f);
        }
    }
}
