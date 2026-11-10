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
        // Raised once when a projectile damages its (still living) target. Audio listens.
        public static event System.Action<Enemy> HitEnemy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            HitEnemy = null;
        }

        [Tooltip("Flight speed in cells per second.")]
        [SerializeField] private float speed = 8f;
        [SerializeField] private float hitDistance = 0.1f;
        [Tooltip("Visual only: how high above the ground the projectile flies in isometric (world units).")]
        [SerializeField] private float isometricHeight = 0.35f;
        [SerializeField] private DepthSorter depth;
        [SerializeField] private SkinnedSprite skinned;
        [Tooltip("Visual only: the short trail of a heavy shot, off unless the tower type asks for it.")]
        [SerializeField] private TrailRenderer trail;
        [Tooltip("Safety net: the projectile is destroyed after this many seconds no matter what.")]
        [SerializeField] private float maxLifetime = 3f;

        private Enemy target;
        private float damage;
        private GridManager grid;
        private Vector2 gridPosition;      // Current position, grid coordinates.
        private Vector2 lastKnownPosition; // Target's last seen position, grid coordinates.
        private bool initialized;
        private GridProjectionType? skinnedFor; // Projection the skinned sprite was last applied for.
        private bool wantsTrail;

        // Visual only: how the shot of this tower type looks. Call before Initialize.
        public void SetLook(TowerSO towerType)
        {
            if (skinned != null)
            {
                skinned.SetSlot(towerType.projectileSlot);
                skinned.SetFallbackLook(towerType.projectileColor, towerType.projectileScale);
            }
            wantsTrail = towerType.projectileTrail;
        }

        public void Initialize(Enemy newTarget, float newDamage, float newSpeed, GridManager gridManager, Vector2 startGridPosition)
        {
            target = newTarget;
            damage = newDamage;
            speed = newSpeed;
            grid = gridManager;
            gridPosition = startGridPosition;
            lastKnownPosition = target != null ? target.GridPosition : gridPosition;
            initialized = grid != null;
            if (initialized)
            {
                PlaceView();
                if (depth != null) depth.Initialize(grid, gridPosition);
                if (trail != null && wantsTrail)
                {
                    trail.Clear();
                    trail.enabled = true;
                }
            }
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
            PlaceView();
            if (depth != null) depth.SetGridPosition(gridPosition);

            if ((gridPosition - lastKnownPosition).sqrMagnitude <= hitDistance * hitDistance)
            {
                if (target != null) // Only a still-living target is hit.
                {
                    Enemy hit = target;
                    target = null; // Never twice, even if Destroy is delayed to the end of the frame.
                    hit.TakeDamage(damage);
                    HitEnemy?.Invoke(hit);
                }
                initialized = false;
                Destroy(gameObject);
            }
        }

        // Visual only: the projected ground point, lifted a little in isometric so the shot
        // flies at body height instead of sliding along the floor.
        private void PlaceView()
        {
            Vector3 world = grid.GridToWorld(gridPosition);
            if (grid.ProjectionType == GridProjectionType.Isometric) world.y += isometricHeight;
            transform.position = world;
            if (skinned != null && skinnedFor != grid.ProjectionType)
            {
                skinned.Apply(grid); // At spawn and after a V switch mid-flight.
                skinnedFor = grid.ProjectionType;
                if (trail != null) trail.Clear(); // No streak across the screen after a V switch.
            }
        }
    }
}
