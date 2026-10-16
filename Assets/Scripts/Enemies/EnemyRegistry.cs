using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Static list of all enemies currently active in the level. Enemies add themselves in
    // OnEnable and remove themselves in OnDisable, so towers and the tower placer can look
    // at "all enemies" without expensive scene searches like FindObjectsByType.
    public static class EnemyRegistry
    {
        private static readonly List<Enemy> active = new List<Enemy>();

        public static IReadOnlyList<Enemy> Active => active;

        public static void Register(Enemy enemy)
        {
            if (enemy != null && !active.Contains(enemy)) active.Add(enemy);
        }

        public static void Unregister(Enemy enemy)
        {
            active.Remove(enemy);
        }

        // Called by GameManager when a level (re)starts.
        public static void Clear()
        {
            active.Clear();
        }

        // Also runs when Play mode starts, even if Domain Reload is disabled (static fields
        // would otherwise keep enemies from the previous play session).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Clear();
        }
    }
}
