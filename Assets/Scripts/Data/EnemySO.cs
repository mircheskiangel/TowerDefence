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
        [Range(0.2f, 1f)] public float scale = 0.5f;
        [Tooltip("Visual only: SkinSet slot for this enemy in the isometric view.")]
        public SkinSlot skinSlot = SkinSlot.Grunt;
        [Tooltip("Visual only: SkinSet slot of the contact shadow in the isometric view.")]
        public SkinSlot skinShadowSlot = SkinSlot.GruntShadow;
        [Tooltip("Visual only: health bar fill width in pixels when skinned (isometric).")]
        [Min(4)] public int healthBarPixels = 18;
    }
}
