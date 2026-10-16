using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // TEMPORARY debug shortcuts for testing (delete this script and its scene object to remove).
    // G: +100 gold.
    public class DebugKeys : MonoBehaviour
    {
        [SerializeField] private PlayerResources resources;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private int goldPerPress = 100;

        private void Start()
        {
            if (resources == null) resources = PlayerResources.Instance;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || resources == null) return;
            if (gameManager != null && !gameManager.IsPlaying) return;

            if (keyboard.gKey.wasPressedThisFrame)
            {
                resources.AddGold(goldPerPress);
                Debug.Log($"[DebugKeys] +{goldPerPress} gold");
            }
        }
    }
}
