using System;
using UnityEngine;

namespace TowerDefense
{
    // The single, global targeting mode used by every tower (including towers placed later).
    // Changing it raises ModeChanged so towers re-scan at once and the HUD updates.
    public static class TargetingSettings
    {
        public static TargetingMode Mode { get; private set; } = TargetingMode.First;

        public static event Action<TargetingMode> ModeChanged;

        public static void SetMode(TargetingMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        // First -> Closest -> Strongest -> Weakest -> First ...
        public static void Cycle()
        {
            int count = Enum.GetValues(typeof(TargetingMode)).Length;
            SetMode((TargetingMode)(((int)Mode + 1) % count));
        }

        // Reset at Play start (static state survives between plays if Domain Reload is disabled).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Mode = TargetingMode.First;
            ModeChanged = null;
        }
    }
}
