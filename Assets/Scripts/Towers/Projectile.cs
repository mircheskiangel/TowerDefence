using UnityEngine;

namespace TowerDefense
{
    // A homing projectile fired by a tower. It flies toward its target's current position and
    // damages it on arrival (simple distance check, no physics). If the target dies or is
    // destroyed mid-flight, it keeps flying to the target's last known position and then
    // disappears without dealing damage. Flight and hit test happen in grid coordinates;
    // the transform is only placed through GridManager's projection.
    public class Projectile : MonoBehaviour
    {
        [Tooltip("Flight speed in cells per second.")]
        [SerializeField] private float speed = 8f;
        [SerializeField] private float hitDistance = 0.1f;
        [Tooltip("Safety net: the projectile is destroyed after this many seconds no matter what.")]
        [SerializeField] private float maxLifetime = 3f;

        private Enemy target;
        private float damage;
        private GridManager grid;
        private Vector2 gridPosition;      // Current position, grid coordinates.
        private Vector2 lastKnownPosition; // Target's last seen position, grid coordinates.
        private bool initialized;

        public void Initialize(Enemy newTarget, float newDamage, float newSpeed, GridManager gridManager, Vector2 startGridPosition)
        {
            target = newTarget;
            damage = newDamage;
            speed = newSpeed;
            grid = gridManager;
            gridPosition = startGridPosition;
            lastKnownPosition = target != null ? target.GridPosition : gridPosition;
            initialized = grid != null;
            if (initialized) transform.position = grid.GridToWorld(gridPosition);
            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            if (!initialized) return;

            // Unity's == treats a destroyed enemy as null. Once the target is gone we forget it,
            // so it can never be damaged later, and keep flying to where it was last seen.
            if (target != null && !target.IsDead)
                lastKnownPosition = target.GridPosition;
            else
                target = null;

            gridPosition = Vector2.MoveTowards(gridPosition, lastKnownPosition, speed * Time.deltaTime);
            transform.position = grid.GridToWorld(gridPosition);

            if ((gridPosition - lastKnownPosition).sqrMagnitude <= hitDistance * hitDistance)
            {
                if (target != null) target.TakeDamage(damage); // Only a still-living target is hit.
                Destroy(gameObject);
            }
        }
    }
}
