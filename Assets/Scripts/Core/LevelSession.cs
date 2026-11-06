using UnityEngine;

namespace TowerDefense
{
    // Which level the next scene load plays. A level change is a scene reload, so the choice has
    // to survive it: this is the only level state that lives outside the scene.
    public static class LevelSession
    {
        public const int None = -1; // No level chosen: the scene starts on the level select screen.

        public static int SelectedIndex { get; private set; } = None;

        public static void Select(int levelIndex)
        {
            SelectedIndex = levelIndex;
        }

        // Every Play session starts on the level select screen (static state survives between
        // plays if Domain Reload is disabled).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetToDefault()
        {
            SelectedIndex = None;
        }
    }
}
