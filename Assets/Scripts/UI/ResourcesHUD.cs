using TMPro;
using UnityEngine;

namespace TowerDefense
{
    // Shows "Gold: X  Lives: Y" in the top-left corner.
    // It does not poll every frame; it redraws only when PlayerResources raises Changed.
    public class ResourcesHUD : MonoBehaviour
    {
        [SerializeField] private PlayerResources resources;
        [SerializeField] private TMP_Text label;

        private void Start()
        {
            if (resources == null) resources = PlayerResources.Instance;
            if (label == null) label = GetComponent<TMP_Text>();
            if (resources == null || label == null)
            {
                Debug.LogError("[ResourcesHUD] Missing references - run Tower Defense/Build Level.");
                enabled = false;
                return;
            }

            resources.Changed += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (resources != null) resources.Changed -= Refresh;
        }

        private void Refresh()
        {
            label.text = $"Gold: {resources.Gold}  Lives: {resources.Lives}";
        }
    }
}
