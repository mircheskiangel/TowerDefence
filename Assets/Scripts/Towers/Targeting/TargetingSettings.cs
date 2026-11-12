using System;
using UnityEngine;

namespace TowerDefense
{
    // The global targeting mode: the strategy every new tower starts with. Changing it (T, the HUD
    // arrows) raises ModeChanged, and every placed tower switches to it too (Tower.SetStrategy).
    // Each tower can then get its own strategy (arrow keys or the tower card, see TowerInfoCard).
    // The one place that defines the order of the strategies: Next / Previous are used by T, the
    // HUD arrows, the arrow keys and the card's < > buttons.
    public static class TargetingSettings
    {
        // First -> Closest -> Strongest -> Weakest, wrapping in both directions.
        private static readonly TargetingMode[] Order =
            { TargetingMode.First, TargetingMode.Closest, TargetingMode.Strongest, TargetingMode.Weakest };

        public static int Count => Order.Length;
        public static TargetingMode At(int index) => Order[index];

        public static int IndexOf(TargetingMode mode)
        {
            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == mode) return i;
            return 0;
        }

        // The strategy 'steps' places after 'mode' in the order (negative = before), wrapping.
        public static TargetingMode Step(TargetingMode mode, int steps)
        {
            int n = Order.Length;
            return Order[((IndexOf(mode) + steps) % n + n) % n];
        }

        public static TargetingMode Next(TargetingMode mode) => Step(mode, 1);
        public static TargetingMode Previous(TargetingMode mode) => Step(mode, -1);

        public static TargetingMode Mode { get; private set; } = TargetingMode.First;

        public static event Action<TargetingMode> ModeChanged;

        public static void SetMode(TargetingMode mode)
        {
            if (Mode == mode) return;
            Mode = mode;
            ModeChanged?.Invoke(mode);
        }

        // T and the HUD's right arrow: the next strategy in the order.
        public static void Cycle()
        {
            SetMode(Next(Mode));
        }

        // Reset at Play start and by GameManager when a level (re)starts
        // (static state survives between plays and reloads if Domain Reload is disabled).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetToDefault()
        {
            Mode = TargetingMode.First;
            ModeChanged = null;
        }
    }
}
