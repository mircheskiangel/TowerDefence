using UnityEngine;

namespace TowerDefense
{
    // A homing projectile fired by a tower. It flies toward its target's current position and
    // damages it on arrival (simple distance check, no physics). If the target dies or is
    // destroyed mid-flight, it keeps flying to the target's last known position and then
    // disappears without dealing damage.
    public class Projectile : MonoBehaviour
    {
        [Tooltip("Flight speed in cells per second.")]
        [SerializeField] private float speed = 8f;
        [SerializeField] private float hitDistance = 0.1f;
        [Tooltip("Safety net: the projectile is destroyed after this many seconds no matter what.")]
        [SerializeField] private float maxLifetime = 3f;

        private Enemy target;
        private float damage;
        private Vector3 lastKnownPosition;
        private bool initialized;

        public void Initialize(Enemy newTarget, float newDamage, float newSpeed)
        {
            target = newTarget;
            damage = newDamage;
            speed = newSpeed;
            lastKnownPosition = target != null ? target.transform.position : transform.position;
            initialized = true;
            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            if (!initialized) return;

            // Unity's == treats a destroyed enemy as null. Once the target is gone we forget it,
            // so it can never be damaged later, and keep flying to where it was last seen.
            if (target != null && !target.IsDead)
                lastKnownPosition = target.transform.position;
            else
                target = null;

            transform.position = Vector3.MoveTowards(transform.position, lastKnownPosition, speed * Time.deltaTime);

            if ((transform.position - lastKnownPosition).sqrMagnitude <= hitDistance * hitDistance)
            {
                if (target != null) target.TakeDamage(damage); // Only a still-living target is hit.
                Destroy(gameObject);
            }
        }
    }
}
