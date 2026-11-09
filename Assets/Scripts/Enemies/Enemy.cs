using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // An enemy that walks from cell center to cell center along an A* path to the goal.
    // Whenever the grid changes it re-paths from the cell it currently occupies,
    // so it reacts to new obstacles mid-walk without teleporting.
    // Its position is kept in GRID coordinates (GridPosition); the transform is only a view of it,
    // placed through GridManager's projection, so movement is identical in any projection.
    public class Enemy : MonoBehaviour
    {
        // Raised once when an enemy is killed by damage (WaveManager / stats can listen).
        public static event Action<Enemy> OnDied;
        // Raised once when an enemy reaches the goal (used for lives in M5). Not a death: no gold.
        public static event Action<Enemy> OnReachedGoal;

        // Static events survive between plays and scene reloads if Domain Reload is disabled.
        // Cleared at Play start and by GameManager when a level (re)starts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ClearStaticEvents()
        {
            OnDied = null;
            OnReachedGoal = null;
        }

        [SerializeField] private SpriteRenderer body;
        [Header("Visuals only (no effect on gameplay)")]
        [SerializeField] private EnemyHitFlash hitFlash;
        [SerializeField] private EnemyHealthBar healthBar;
        [SerializeField] private ProjectionLook look;   // Visual: flat circle / standing circle + shadow.
        [SerializeField] private DepthSorter depth;     // Visual: isometric draw order.
        [SerializeField] private SkinnedSprite shadowSkin; // Visual: isometric contact shadow (slot per type).

        // Raised whenever this enemy takes damage: (current health, max health). Used by the health bar.
        public event Action<float, float> HealthChanged;

        private EnemySO data;
        private float maxHealth;
        private bool elite;
        private GridManager grid;
        private AStarPathfinder pathfinder;

        private List<Vector2Int> path;  // Cells from (roughly) our position to the goal.
        private int targetIndex;        // Index in 'path' of the cell center we are walking towards.
        private bool hasRoute;          // False while no path to the goal exists (we wait).
        private Vector2 gridPosition;   // Logical position in continuous grid coordinates.
        private bool subscribed;

        public EnemySO Data => data;
        public float CurrentHealth { get; private set; }
        public float MaxHealth => maxHealth;
        public bool IsElite => elite;
        // Visual only: the walk frames this enemy is drawn with (Characters style).
        public SkinSlot WalkSlot => EliteOr(data.eliteWalkSlot, data.walkSlot);
        public bool IsDead { get; private set; }
        public bool IsWaiting => !hasRoute;

        // Logical position in continuous grid coordinates (integer values = cell centers).
        public Vector2 GridPosition => gridPosition;

        // The cell whose square contains the enemy right now.
        public Vector2Int CurrentCell => GridManager.CellAt(gridPosition);

        // How far along its route the enemy is: higher = closer to the goal (0 = at the goal).
        // Defined as minus the remaining path length, NOT as distance walked, because after a
        // re-path an enemy that took a long detour may have walked more but still be further away.
        public float PathProgress => -RemainingDistance;

        // Distance left to the goal along the current path, in cells (used by "First" targeting).
        public float RemainingDistance
        {
            get
            {
                if (!hasRoute || path == null) return float.MaxValue;
                if (targetIndex >= path.Count) return 0f;
                float toNext = Vector2.Distance(gridPosition, path[targetIndex]);
                return toNext + (path.Count - 1 - targetIndex);
            }
        }

        // healthMultiplier and isElite come from the wave group: the base stats in EnemySO stay the
        // same for everyone. Elite is the look only; its strength is the health multiplier.
        public void Initialize(EnemySO enemyData, GridManager gridManager, AStarPathfinder sharedPathfinder,
            float healthMultiplier = 1f, bool isElite = false)
        {
            data = enemyData;
            elite = isElite;
            maxHealth = data.maxHealth * healthMultiplier;
            grid = gridManager;
            pathfinder = sharedPathfinder;

            CurrentHealth = maxHealth;
            name = data.displayName;
            // Start where we were spawned, converted once into grid coordinates.
            gridPosition = grid.WorldToGrid(transform.position);
            transform.position = grid.GridToWorld(gridPosition);
            transform.localScale = Vector3.one * data.scale * (elite ? data.eliteScale : 1f) * grid.CellSize;
            Color tint = elite ? data.eliteColor : data.color;
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (body != null) body.color = tint;
            if (look != null) look.SetTint(tint);
            ApplySkinSlots();
            if (healthBar != null)
            {
                int barPixels = elite && data.eliteHealthBarPixels > 0 ? data.eliteHealthBarPixels : data.healthBarPixels;
                healthBar.SetSkinnedWidth(barPixels);
                healthBar.SetPlainWidthScale((float)barPixels / data.healthBarPixels * data.topDownBarScale);
            }
            if (look != null) look.Apply(grid);           // Before the bar: it sits on top of the current sprite.
            if (hitFlash != null) hitFlash.SetBaseColor(body != null ? body.color : tint); // Tint or untinted skin.
            if (healthBar != null) healthBar.ResetBar(); // After the scale is set (keeps a fixed bar size).
            if (depth != null) depth.Initialize(grid, gridPosition);

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
            grid.ProjectionChanged += SnapViewToGrid;
            EnemySkinSettings.Changed += RefreshSkin;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed) return;
            if (grid != null)
            {
                grid.GridChanged -= Repath;
                grid.ProjectionChanged -= SnapViewToGrid;
            }
            EnemySkinSettings.Changed -= RefreshSkin;
            subscribed = false;
        }

        // Visual only: after a projection switch, put the transform where our grid position is
        // drawn now (Update does this every frame while walking, but a waiting enemy does not move).
        private void SnapViewToGrid()
        {
            if (this == null)
            {
                if (grid != null) grid.ProjectionChanged -= SnapViewToGrid;
                return;
            }
            if (grid == null) return;
            transform.position = grid.GridToWorld(gridPosition);
            if (look != null) look.Apply(grid);
            if (hitFlash != null && body != null) hitFlash.SetBaseColor(body.color);
            if (healthBar != null) healthBar.Layout();
            if (depth != null) depth.Refresh();
        }

        // Visual only: the isometric sprite and shadow slots for the current enemy skin style.
        private void ApplySkinSlots()
        {
            bool characters = EnemySkinSettings.Style == EnemySkinStyle.Characters;
            if (look != null) look.SetIsometricSlot(characters ? WalkSlot : EliteOr(data.eliteSkinSlot, data.skinSlot));
            if (shadowSkin != null)
                shadowSkin.SetSlot(characters ? EliteOr(data.eliteCharacterShadowSlot, data.characterShadowSlot)
                                              : EliteOr(data.eliteSkinShadowSlot, data.skinShadowSlot));
        }

        private SkinSlot EliteOr(SkinSlot eliteSlot, SkinSlot normalSlot)
        {
            return elite && eliteSlot != SkinSlot.None ? eliteSlot : normalSlot;
        }

        // Visual only: the K key switched the skin style.
        private void RefreshSkin()
        {
            if (this == null || data == null) return;
            ApplySkinSlots();
            if (look != null) look.Apply(grid);
            if (hitFlash != null && body != null) hitFlash.SetBaseColor(body.color);
            if (healthBar != null) healthBar.Layout();
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
        // (excluding a's center itself), i.e. we are already heading from a to b. Grid units.
        private bool IsOnSegment(Vector2Int a, Vector2Int b)
        {
            Vector2 start = a;
            Vector2 segment = (Vector2)b - start;
            float t = Vector2.Dot(gridPosition - start, segment) / segment.sqrMagnitude;
            if (t <= 0f || t > 1f) return false;
            Vector2 closest = start + segment * t;
            return (gridPosition - closest).sqrMagnitude < 0.0001f;
        }

        private void Update()
        {
            if (IsDead || path == null || grid == null) return;

            if (targetIndex >= path.Count)
            {
                if (hasRoute) ReachGoal(); // The last cell of a valid path is the goal.
                return;                    // Otherwise: no route, wait in place.
            }

            // Move in grid units (moveSpeed is in cells per second), then show the result.
            Vector2 target = path[targetIndex];
            gridPosition = Vector2.MoveTowards(gridPosition, target, data.moveSpeed * Time.deltaTime);
            transform.position = grid.GridToWorld(gridPosition);
            if (depth != null) depth.SetGridPosition(gridPosition);

            if ((gridPosition - target).sqrMagnitude < 0.000001f)
                targetIndex++;
        }

        // Ignored once the enemy is dead, so projectiles still in flight do nothing.
        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
            HealthChanged?.Invoke(CurrentHealth, MaxHealth);
            if (CurrentHealth <= 0f) Die();
            else if (hitFlash != null) hitFlash.Flash(); // Only survivors flash; the dead vanish at once.
        }

        // Runs exactly once: IsDead is set first, so any later TakeDamage (or a second Die)
        // returns immediately and the gold reward can never be paid twice.
        private void Die()
        {
            if (IsDead) return;
            Deactivate();
            // Visual only (isometric skin): a fading copy of the ball and its shadow; the enemy
            // itself is removed exactly as before.
            if (look != null && look.IsSkinned) VisualFade.Spawn(data.deathFadeSeconds, 1.3f, body, shadowSkin != null ? shadowSkin.GetComponent<SpriteRenderer>() : null);
            OnDied?.Invoke(this);
            if (PlayerResources.Instance != null) PlayerResources.Instance.AddGold(data.goldReward);
            Destroy(gameObject);
        }

        // Reaching the goal is not a death: no gold. M5 will take a life via OnReachedGoal.
        private void ReachGoal()
        {
            if (IsDead) return;
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
