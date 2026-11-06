using System;
using UnityEngine;

namespace TowerDefense
{
    // Which levels are unlocked: a level opens when the one before it has been WON. Stored in
    // PlayerPrefs as the number of levels won in order. Every PlayerPrefs call is wrapped: if it
    // fails the game keeps working, and the unlock lasts for this session only.
    public static class LevelProgress
    {
        private const string Key = "TowerDefense.LevelsWon";

        private static int sessionWon;

        // Off during BalanceBot batches, so automated wins never change the player's progress.
        public static bool Recording { get; set; } = true;

        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStaticState()
        {
            sessionWon = 0;
            Recording = true;
            Changed = null;
        }

        public static bool IsUnlocked(int levelIndex)
        {
            return levelIndex <= 0 || LevelsWon() >= levelIndex;
        }

        // Called when a level is won (never when it is lost).
        public static void MarkWon(int levelIndex)
        {
            if (Recording) Store(levelIndex + 1);
        }

        public static void UnlockAll(int levelCount)
        {
            Store(levelCount);
        }

        public static void ResetProgress()
        {
            sessionWon = 0;
            try
            {
                PlayerPrefs.DeleteKey(Key);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LevelProgress] Could not reset the saved progress: " + e.Message);
            }
            Changed?.Invoke();
        }

        private static void Store(int won)
        {
            if (won <= LevelsWon()) return;
            sessionWon = won;
            try
            {
                PlayerPrefs.SetInt(Key, won);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LevelProgress] Could not save the progress: " + e.Message);
            }
            Changed?.Invoke();
        }

        private static int LevelsWon()
        {
            int stored = 0;
            try
            {
                stored = PlayerPrefs.GetInt(Key, 0);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LevelProgress] Could not read the saved progress: " + e.Message);
            }
            return Mathf.Max(stored, sessionWon);
        }
    }
}
