using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Input for targeting: pressing T cycles the global targeting mode of all towers.
    public class TargetingModeSwitcher : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tKey.wasPressedThisFrame)
            {
                TargetingSettings.Cycle();
                Debug.Log($"[Targeting] Mode: {TargetingSettings.Mode}");
            }
        }
    }
}
