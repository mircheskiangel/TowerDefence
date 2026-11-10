using UnityEngine;

namespace TowerDefense
{
    // Data for one enemy type (e.g. Grunt, Tank). The same Enemy prefab is reused for
    // every type; it reads its stats and look from one of these assets at spawn time.
    [CreateAssetMenu(menuName = "Tower Defense/Enemy Data", fileName = "NewEnemy")]
    public class EnemySO : ScriptableObject
    {
        public string displayName = "Enemy";
        [Min(1f)] public float maxHealth = 30f;
        [Tooltip("Movement speed in cells per second.")]
        [Min(0.1f)] public float moveSpeed = 2f;
        [Min(0)] public int goldReward = 5;
        public Color color = Color.white;
        [Tooltip("Visual size relative to one cell.")]
        [Range(0.2f, 2f)] public float scale = 0.5f;
        [Tooltip("Visual only: SkinSet slot for this enemy in the isometric view.")]
        public SkinSlot skinSlot = SkinSlot.Grunt;
        [Tooltip("Visual only: SkinSet slot of the contact shadow in the isometric view.")]
        public SkinSlot skinShadowSlot = SkinSlot.GruntShadow;
        [Tooltip("Visual only: SkinSet slot of the character walk frames (Characters style).")]
        public SkinSlot walkSlot = SkinSlot.GruntWalk;
        [Tooltip("Visual only: SkinSet slot of the character ground shadow (Characters style).")]
        public SkinSlot characterShadowSlot = SkinSlot.GruntCharacterShadow;
        [Tooltip("Visual only: seconds per walk frame at this enemy's normal speed.")]
        [Min(0.01f)] public float walkFrameSeconds = 0.18f;
        [Tooltip("Visual only: health bar fill width in pixels when skinned (isometric).")]
        [Min(4)] public int healthBarPixels = 18;
        [Tooltip("Audio only: played when a projectile hits it and when it dies.")]
        public SfxId hitSound = SfxId.HitGrunt;
        public SfxId deathSound = SfxId.Death;

        [Header("Elite look (visual only; empty slots keep the normal look)")]
        public SkinSlot eliteSkinSlot = SkinSlot.None;
        public SkinSlot eliteSkinShadowSlot = SkinSlot.None;
        public SkinSlot eliteWalkSlot = SkinSlot.None;
        public SkinSlot eliteCharacterShadowSlot = SkinSlot.None;
        public Color eliteColor = Color.white;
        [Tooltip("Top-down: size relative to the normal scale (isometric sprites are drawn larger).")]
        [Min(0.1f)] public float eliteScale = 1f;
        [Tooltip("Health bar fill width in pixels when skinned; 0 = the normal width.")]
        [Min(0)] public int eliteHealthBarPixels;

        [Header("Boss")]
        [Tooltip("Shows the large boss bar and its announcement while it is alive.")]
        public bool isBoss;
        [Tooltip("Reaching the goal costs all remaining lives, not just one.")]
        public bool instantDefeatOnLeak;
        [Tooltip("Shown on the Lost panel after an instant defeat.")]
        public string defeatMessage = "";
        [Tooltip("Visual only: seconds of the death fade.")]
        [Min(0.01f)] public float deathFadeSeconds = 0.25f;
        [Tooltip("Visual only: top-down head bar width relative to the normal one.")]
        [Min(0.1f)] public float topDownBarScale = 1f;
    }
}
