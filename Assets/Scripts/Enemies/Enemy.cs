using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // An enemy that walks from cell center to cell center along an A* path to the goal.
    // Whenever the grid changes it re-paths from the cell it currently occupies,
    // so it reacts to new obstacles mid-walk without teleporting.
    public class Enemy : MonoBehaviour
    {
        // Every enemy currently alive in the level (used by tower placement checks and, later, targeting).
        private static readonly List<Enemy> active = new List<Enemy>();
        public static IReadOnlyList<Enemy> Active => active;

        // Clears the list when Play mode starts (needed if domain reload is disabled in the editor).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active.Clear();
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
        public bool IsDead { get; private set; }
        public bool IsWaiting => !hasRoute;

        // The cell whose square contains the enemy right now.
        public Vector2Int CurrentCell => grid != null ? grid.WorldToCell(transform.position) : Vector2Int.zero;

        // Total distance walked since spawning, in cells.
        public float PathProgress { get; private set; }

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

            if (!active.Contains(this)) active.Add(this);
            Subscribe();
            Repath();
        }

        private void OnEnable()
        {
            // Re-subscribe if the object was disabled and enabled again after Initialize.
            if (grid != null) Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            active.Remove(this);
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
            PathProgress += Vector3.Distance(transform.position, newPosition) / grid.CellSize;
            transform.position = newPosition;

            if ((newPosition - target).sqrMagnitude < 0.000001f)
                targetIndex++;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth -= amount;
            if (CurrentHealth <= 0f) Die();
        }

        private void Die()
        {
            IsDead = true;
            Unsubscribe();
            active.Remove(this);
            Destroy(gameObject);
        }

        private void ReachGoal()
        {
            Debug.Log($"[Enemy] {name} reached goal");
            IsDead = true;
            Unsubscribe();
            active.Remove(this);
            Destroy(gameObject);
        }
    }
}
