using System;
using UnityEngine;

namespace TowerDefense
{
    // How enemies are drawn in the isometric view (visual only).
    public enum EnemySkinStyle
    {
        Characters, // Goblin and ogre with a 2-frame walk animation.
        Balls       // Glossy balls with a vertical bob.
    }

    // The global enemy skin style. K switches it at runtime for all live and future enemies.
    public static class EnemySkinSettings
    {
        public static EnemySkinStyle Style { get; private set; } = EnemySkinStyle.Characters;

        public static event Action Changed;

        public static void SetStyle(EnemySkinStyle style)
        {
            if (style == Style) return;
            Style = style;
            Changed?.Invoke();
        }

        public static void Toggle()
        {
            SetStyle(Style == EnemySkinStyle.Characters ? EnemySkinStyle.Balls : EnemySkinStyle.Characters);
        }

        // Back to the default at Play start (static state survives between plays and reloads if
        // Domain Reload is disabled).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetToDefault()
        {
            Style = EnemySkinStyle.Characters;
            Changed = null;
        }
    }
}
