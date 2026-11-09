using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // One level, described as data: board size, spawn and goal, permanent walls, starting
    // resources, waves and the seed of the decorative ring. LevelBuilder generates the assets;
    // LevelManager applies the selected one when the scene starts.
    [CreateAssetMenu(menuName = "Tower Defense/Level Data", fileName = "NewLevel")]
    public class LevelData : ScriptableObject
    {
        // A tower that stands on its cell from the start of the level and can never be changed.
        [Serializable]
        public struct FixedTower
        {
            public TowerSO tower;
            public Vector2Int cell;
        }

        // At most maxCount live towers of this type may be bought on the level.
        [Serializable]
        public struct TowerLimit
        {
            public TowerSO tower;
            [Min(1)] public int maxCount;
        }

        public string levelName = "Level";
        [Min(1)] public int width = 16;
        [Min(1)] public int height = 10;
        public Vector2Int spawnCell = new Vector2Int(0, 5);
        public Vector2Int goalCell = new Vector2Int(15, 4);
        [Tooltip("Permanent walls: blocked for A*, cannot be built on, sold or replaced.")]
        public List<Vector2Int> wallCells = new List<Vector2Int>();
        [Min(0)] public int startingGold = 100;
        [Min(1)] public int startingLives = 10;
        public List<WaveSO> waves = new List<WaveSO>();
        [Tooltip("Fixed seed of the decoration on the ring around the board.")]
        public int decorationSeed = 11;
        [Tooltip("The tower types the player may buy: the only authority on what the build UI offers.")]
        public List<TowerSO> allowedTowers = new List<TowerSO>();
        [Tooltip("Towers placed at the start: permanent, free, blocking their cell like walls.")]
        public List<FixedTower> fixedTowers = new List<FixedTower>();
        [Tooltip("Limits on bought towers; a type that is not listed has no limit.")]
        public List<TowerLimit> towerLimits = new List<TowerLimit>();

        // The most towers of this type the player may have, or 0 if there is no limit.
        public int LimitFor(TowerSO tower)
        {
            foreach (TowerLimit limit in towerLimits)
                if (limit.tower == tower) return limit.maxCount;
            return 0;
        }
    }
}
