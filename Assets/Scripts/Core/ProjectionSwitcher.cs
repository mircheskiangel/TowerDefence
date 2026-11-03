using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Input for the view: pressing V switches the board between top-down and isometric.
    // Only visuals change (GridManager.ProjectionChanged); the game keeps running unchanged.
    public class ProjectionSwitcher : MonoBehaviour
    {
        [SerializeField] private GridManager grid;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (grid == null || keyboard == null || !keyboard.vKey.wasPressedThisFrame) return;

            GridProjectionType next = grid.ProjectionType == GridProjectionType.TopDown
                ? GridProjectionType.Isometric
                : GridProjectionType.TopDown;
            grid.SetProjection(next);
            Debug.Log($"[View] Projection: {next}");
        }
    }
}
