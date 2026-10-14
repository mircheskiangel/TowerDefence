using TMPro;
using UnityEngine;

namespace TowerDefense
{
    // Shows the current global targeting mode, e.g. "Targeting: First (T to change)".
    // Redraws only when TargetingSettings.ModeChanged fires.
    public class TargetingHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private void OnEnable()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            TargetingSettings.ModeChanged += Refresh;
            Refresh(TargetingSettings.Mode);
        }

        private void OnDisable()
        {
            TargetingSettings.ModeChanged -= Refresh;
        }

        private void Refresh(TargetingMode mode)
        {
            if (label != null) label.text = $"Targeting: {mode} (T to change)";
        }
    }
}
