using UnityEngine;

namespace TowerDefense
{
    // Data for one tower type: cost and combat stats. The Tower prefab reads one of
    // these when it is placed, so new tower types can be added as assets without code.
    [CreateAssetMenu(menuName = "Tower Defense/Tower Data", fileName = "NewTower")]
    public class TowerSO : ScriptableObject
    {
        public string displayName = "Tower";
        [Min(0)] public int cost = 25;
        [Tooltip("Attack range in world units (1 unit = 1 cell).")]
        [Min(0.5f)] public float range = 2.5f;
        [Min(0f)] public float damage = 10f;
        [Tooltip("Shots per second.")]
        [Min(0.1f)] public float fireRate = 1.5f;
        [Tooltip("Projectile speed in cells per second.")]
        [Min(0.5f)] public float projectileSpeed = 8f;
        public Color color = Color.white;

        [Header("Visual only")]
        [Tooltip("The body of a bought tower; also its icon and placement preview.")]
        public SkinSlot bodySlot = SkinSlot.TowerBody;
        [Tooltip("The body when the tower is one of a level's fixed towers.")]
        public SkinSlot fixedBodySlot = SkinSlot.TowerBody;
        public SkinSlot shadowSlot = SkinSlot.TowerShadow;
        public SkinSlot projectileSlot = SkinSlot.Projectile;
        [Tooltip("Audio only: the sound of a shot.")]
        public SfxId fireSound = SfxId.ShootBasic;
        [Tooltip("Top-down: the projectile's tint.")]
        public Color projectileColor = new Color(1.00f, 0.92f, 0.25f);
        [Tooltip("Top-down: the projectile's size relative to the normal one.")]
        [Min(0.1f)] public float projectileScale = 1f;
        public bool projectileTrail;
        [Tooltip("Scale multiplier of the body for a short 'kick' when the tower fires.")]
        [Min(1f)] public float fireKickScale = 1.1f;
        [Tooltip("Top-down: size of the square relative to the normal tower.")]
        [Min(0.1f)] public float topDownScale = 1f;
    }
}
