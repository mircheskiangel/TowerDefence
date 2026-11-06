using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // Input for the enemy skin: pressing K switches between characters and balls (visual only).
    public class EnemySkinSwitcher : MonoBehaviour
    {
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.kKey.wasPressedThisFrame) return;

            EnemySkinSettings.Toggle();
            Debug.Log($"[View] Enemy skin: {EnemySkinSettings.Style}");
        }
    }
}
