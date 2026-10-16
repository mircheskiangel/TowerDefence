using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // An enemy that walks from cell center to cell center along an A* path to the goal.
    // Whenever the grid changes it re-paths from the cell it currently occupies,
    // so it reacts to new obstacles mid-walk without teleporting.
    public class Enemy : MonoBehaviour
    {
        // Raised once when an enemy is killed by damage (WaveManager / stats can listen).
        public static event Action<Enemy> OnDied;
        // Raised once when an enemy reaches the goal (used for lives in M5). Not a death: no gold.
        public static event Action<Enemy> OnReachedGoal;

        // Static events survive between plays if Domain Reload is disabled, so clear them at Play start.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            OnDied = null;
            OnReachedGoal = null;
        }

        [SerializeField] private SpriteRenderer body;

        private EnemySO data;
        private GridManager grid;
        private AStarPathfinder pathfinder;

        private List<Vector2Int> path;  // Cells from (roughly) our position to the goal.
        private int targetIndex;        // Index in 'path' of the cell center we are walking towards.
        private bool hasRoute;          // False while no path to the goal exists (we wait).
        private bool subscribed;

        public EnemySO Data => data;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => data != null ? data.maxHealth : 0f;
        public bool IsDead { get; private set; }
        public bool IsWaiting => !hasRoute;

        // The cell whose square contains the enemy right now.
        public Vector2Int CurrentCell => grid != null ? grid.WorldToCell(transform.position) : Vector2Int.zero;

        // How far along its route the enemy is: higher = closer to the goal (0 = at the goal).
        // Defined as minus the remaining path length, NOT as distance walked, because after a
        // re-path an enemy that took a long detour may have walked more but still be further away.
        public float PathProgress => -RemainingDistance;

        // Total distance walked since spawning, in cells (for stats/debugging).
        public float DistanceWalked { get; private set; }

        // Distance left to the goal along the current path, in cells (used by "First" targeting).
        public float RemainingDistance
        {
            get
            {
                if (!hasRoute || path == null) return float.MaxValue;
                if (targetIndex >= path.Count) return 0f;
                float toNext = Vector3.Distance(transform.position, grid.CellToWorld(path[targetIndex])) / grid.CellSize;
                return toNext + (path.Count - 1 - targetIndex);
            }
        }

        public void Initialize(EnemySO enemyData, GridManager gridManager, AStarPathfinder sharedPathfinder)
        {
            data = enemyData;
            grid = gridManager;
            pathfinder = sharedPathfinder;

            CurrentHealth = data.maxHealth;
            name = data.displayName;
            transform.localScale = Vector3.one * data.scale * grid.CellSize;
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (body != null) body.color = data.color;

            Subscribe();
            Repath();
        }

        private void OnEnable()
        {
            EnemyRegistry.Register(this);
            // Re-subscribe if the object was disabled and enabled again after Initialize.
            if (grid != null) Subscribe();
        }

        private void OnDisable()
        {
            EnemyRegistry.Unregister(this);
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (subscribed || grid == null) return;
            grid.GridChanged += Repath;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (grid != null) grid.GridChanged -= Repath;
            subscribed = false;
        }

        // Recomputes the path from the cell we occupy now to the goal.
        // The current cell may itself be blocked (a tower was just built on top of us):
        // A* always treats the start cell as walkable, so we simply walk out of it.
        // To avoid snapping backwards: if we are already part-way along the step from
        // that cell towards the new path's next cell, we keep walking forwards to it;
        // otherwise we first walk back to our cell's center (at most half a cell).
        public void Repath()
        {
            // Safety net: if Unity destroyed us without OnDestroy running (e.g. DestroyImmediate
            // in edit mode), drop the stale subscription instead of touching a dead object.
            if (this == null)
            {
                if (grid != null) grid.GridChanged -= Repath;
                return;
            }
            if (IsDead || grid == null || pathfinder == null) return;

            Vector2Int from = CurrentCell;
            List<Vector2Int> newPath = pathfinder.FindPath(from, grid.GoalCell);

            if (newPath == null)
            {
                if (hasRoute) Debug.Log($"[Enemy] {name} has no path to the goal - waiting.");
                hasRoute = false;
                // Settle on the center of the current cell and wait for the next GridChanged.
                path = new List<Vector2Int> { from };
                targetIndex = 0;
                return;
            }

            path = newPath;
            hasRoute = true;
            targetIndex = (path.Count > 1 && IsOnSegment(path[0], path[1])) ? 1 : 0;
        }

        // True if our position lies on the straight segment between the centers of cells a and b
        // (excluding a's center itself), i.e. we are already heading from a to b.
        private bool IsOnSegment(Vector2Int a, Vector2Int b)
        {
            Vector3 start = grid.CellToWorld(a);
            Vector3 end = grid.CellToWorld(b);
            Vector3 segment = end - start;
            float t = Vector3.Dot(transform.position - start, segment) / segment.sqrMagnitude;
            if (t <= 0f || t > 1f) return false;
            Vector3 closest = start + segment * t;
            return (transform.position - closest).sqrMagnitude < 0.0001f;
        }

        private void Update()
        {
            if (IsDead || path == null || grid == null) return;

            if (targetIndex >= path.Count)
            {
                if (hasRoute) ReachGoal(); // The last cell of a valid path is the goal.
                return;                    // Otherwise: no route, wait in place.
            }

            Vector3 target = grid.CellToWorld(path[targetIndex]);
            float step = data.moveSpeed * grid.CellSize * Time.deltaTime;
            Vector3 newPosition = Vector3.MoveTowards(transform.position, target, step);
            DistanceWalked += Vector3.Distance(transform.position, newPosition) / grid.CellSize;
            transform.position = newPosition;

            if ((newPosition - target).sqrMagnitude < 0.000001f)
                targetIndex++;
        }

        // Ignored once the enemy is dead, so projectiles still in flight do nothing.
        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            if (CurrentHealth <= 0f) Die();
        }

        // Runs exactly once: IsDead is set first, so any later TakeDamage (or a second Die)
        // returns immediately and the gold reward can never be paid twice.
        private void Die()
        {
            if (IsDead) return;
            Deactivate();
            OnDied?.Invoke(this);
            if (PlayerResources.Instance != null) PlayerResources.Instance.AddGold(data.goldReward);
            Destroy(gameObject);
        }

        // Reaching the goal is not a death: no gold. M5 will take a life via OnReachedGoal.
        private void ReachGoal()
        {
            if (IsDead) return;
            Debug.Log($"[Enemy] {name} reached goal");
            Deactivate();
            OnReachedGoal?.Invoke(this);
            Destroy(gameObject);
        }

        // Marks the enemy as finished right away. Destroy() only happens at the end of the frame,
        // so we also leave the registry now: towers must not pick it on their next scan.
        private void Deactivate()
        {
            IsDead = true;
            EnemyRegistry.Unregister(this);
            Unsubscribe();
        }
    }
}
