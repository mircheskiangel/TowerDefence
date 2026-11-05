using UnityEngine;

namespace TowerDefense
{
    // Visual only: a set of sprites for the ISOMETRIC view (the top-down view never uses skins).
    // Every slot is optional. An empty slot means "keep the sprite the object already has", so
    // an empty SkinSet looks exactly like the built-in generated sprites.
    // LevelBuilder creates Assets/Art/DefaultSkin.asset and fills it from the generated PNG files
    // (see SkinLoader); consumers ask for a slot through Resolve(), always with a fallback.
    [CreateAssetMenu(menuName = "Tower Defense/Skin Set", fileName = "NewSkin")]
    public class SkinSet : ScriptableObject
    {
        [Header("Floor")]
        public Sprite[] grassTiles = new Sprite[0];
        public Sprite[] pathTiles = new Sprite[0];
        public Sprite cliffLeft;
        public Sprite cliffRight;

        [Header("Spawn and goal")]
        public Sprite spawnPortal;
        public Sprite goalGate;

        [Header("Tower")]
        public Sprite towerBody;
        public Sprite towerBase;
        public Sprite towerShadow;
        public Sprite projectile;

        [Header("Enemies")]
        public Sprite grunt;
        public Sprite tank;

        [Header("Decoration")]
        public Sprite[] trees = new Sprite[0];
        public Sprite[] rocks = new Sprite[0];
        public Sprite[] bushes = new Sprite[0];

        [Header("HUD")]
        public Sprite hudPanel;
        public Sprite hudButton;

        // The sprite in 'slot' (variant 'variant', wrapped for variant slots), or null if empty.
        public Sprite Get(SkinSlot slot, int variant = 0)
        {
            switch (slot)
            {
                case SkinSlot.GrassTile: return Pick(grassTiles, variant);
                case SkinSlot.PathTile: return Pick(pathTiles, variant);
                case SkinSlot.SpawnPortal: return spawnPortal;
                case SkinSlot.GoalGate: return goalGate;
                case SkinSlot.TowerBody: return towerBody;
                case SkinSlot.TowerBase: return towerBase;
                case SkinSlot.TowerShadow: return towerShadow;
                case SkinSlot.Grunt: return grunt;
                case SkinSlot.Tank: return tank;
                case SkinSlot.Tree: return Pick(trees, variant);
                case SkinSlot.Rock: return Pick(rocks, variant);
                case SkinSlot.Bush: return Pick(bushes, variant);
                case SkinSlot.CliffLeft: return cliffLeft;
                case SkinSlot.CliffRight: return cliffRight;
                case SkinSlot.Projectile: return projectile;
                case SkinSlot.HudPanel: return hudPanel;
                case SkinSlot.HudButton: return hudButton;
                default: return null;
            }
        }

        // Number of variants in 'slot' (0 = empty, 1 for single-sprite slots that are filled).
        public int VariantCount(SkinSlot slot)
        {
            switch (slot)
            {
                case SkinSlot.GrassTile: return Count(grassTiles);
                case SkinSlot.PathTile: return Count(pathTiles);
                case SkinSlot.Tree: return Count(trees);
                case SkinSlot.Rock: return Count(rocks);
                case SkinSlot.Bush: return Count(bushes);
                default: return Get(slot) != null ? 1 : 0;
            }
        }

        public bool Has(SkinSlot slot) => VariantCount(slot) > 0;

        // The skin's sprite for 'slot', or 'fallback' when there is no skin or the slot is empty.
        public static Sprite Resolve(SkinSet skin, SkinSlot slot, Sprite fallback, int variant = 0)
        {
            if (skin == null || slot == SkinSlot.None) return fallback;
            Sprite sprite = skin.Get(slot, variant);
            return sprite != null ? sprite : fallback;
        }

        // Empties every slot (LevelBuilder refills it from the generated files).
        public void Clear()
        {
            grassTiles = new Sprite[0];
            pathTiles = new Sprite[0];
            trees = new Sprite[0];
            rocks = new Sprite[0];
            bushes = new Sprite[0];
            cliffLeft = cliffRight = null;
            spawnPortal = goalGate = null;
            towerBody = towerBase = towerShadow = projectile = null;
            grunt = tank = null;
            hudPanel = hudButton = null;
        }

        private static Sprite Pick(Sprite[] variants, int variant)
        {
            int count = Count(variants);
            if (count == 0) return null;
            int i = ((variant % count) + count) % count;
            return variants[i];
        }

        private static int Count(Sprite[] variants) => variants != null ? variants.Length : 0;
    }
}
