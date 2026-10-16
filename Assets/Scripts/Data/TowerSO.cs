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
    }
}
