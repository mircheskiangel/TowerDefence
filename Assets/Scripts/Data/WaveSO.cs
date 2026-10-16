using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // One wave of enemies, described as data: an ordered list of spawn groups,
    // e.g. "6 Grunts every 1.2 s, then after 2 s, 2 Tanks every 3 s".
    // WaveManager plays these assets one after another.
    [CreateAssetMenu(menuName = "Tower Defense/Wave Data", fileName = "NewWave")]
    public class WaveSO : ScriptableObject
    {
        [Serializable]
        public class SpawnGroup
        {
            public EnemySO enemy;
            [Min(1)] public int count = 5;
            [Tooltip("Seconds between two enemies of this group.")]
            [Min(0f)] public float spawnInterval = 1f;
            [Tooltip("Seconds to wait before this group starts spawning.")]
            [Min(0f)] public float delayBefore = 0f;
        }

        public List<SpawnGroup> groups = new List<SpawnGroup>();

        public int TotalEnemies
        {
            get
            {
                int total = 0;
                foreach (SpawnGroup group in groups) total += group.count;
                return total;
            }
        }
    }
}
