using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // A placed tower. Every scanInterval seconds it collects the enemies within its range
    // and asks the selector for its own strategy (Strategy) which one to target. A tower starts
    // with the global TargetingSettings.Mode and follows it when it changes (T, HUD arrows);
    // SetStrategy gives it its own (arrow keys, the tower card).
    // It fires a projectile at that target whenever its cooldown (1 / fireRate) is over.
    // Range checks use grid coordinates (the tower's Cell vs. Enemy.GridPosition), never world distances.
    // One prefab for every type: stats and look come from TowerSO. A fixed tower only differs by what
    // the level does with it (see LevelData.fixedTowers) and its gold mark: part of its sprite in
    // isometric (TowerSO.fixedBodySlot), a small dot in top-down. A bought tower has neither.
    public class Tower : MonoBehaviour
    {
        // Raised after a tower fires a projectile (audio listens; gameplay does not).
        public static event System.Action<Tower> Fired;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticEvents()
        {
            Fired = null;
        }

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private ProjectionLook look;   // Visual: flat square / standing tower.
        [SerializeField] private DepthSorter depth;     // Visual: isometric draw order.
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private float scanInterval = 0.1f;
        [Header("Visuals only (no effect on gameplay)")]
        [SerializeField] private float fireKickDuration = 0.05f;
        [SerializeField] private SkinnedSprite shadowSkin;
        [Tooltip("Top-down only: the gold dot of a fixed tower.")]
        [SerializeField] private GameObject fixedMark;

        private readonly List<Enemy> inRange = new List<Enemy>(); // Reused every scan: no garbage.
        private Enemy currentTarget;
        private float scanTimer;
        private float fireCooldown;
        private Vector3 baseScale = Vector3.one;
        private Transform KickTarget => body != null ? body.transform : transform;
        private float kickTimeLeft;

        private GridManager grid;

        public TowerSO Data { get; private set; }
        // This tower's targeting strategy (the Strategy pattern: TargetSelectors.Get(Strategy)).
        public TargetingMode Strategy { get; private set; } = TargetingMode.First;
        // Raised when this tower's strategy changes (the tower card listens).
        public event System.Action<Tower> StrategyChanged;
        public Vector2Int Cell { get; private set; }
        public bool IsFixed { get; private set; }

        // The enemy this tower is aiming at, or null. Unity's overloaded == makes a destroyed
        // enemy compare equal to null, so a dead target is never returned.
        public Enemy CurrentTarget => currentTarget != null && !currentTarget.IsDead ? currentTarget : null;

        public void Initialize(TowerSO data, Vector2Int cell, GridManager gridManager, bool isFixed = false)
        {
            Data = data;
            Cell = cell;
            IsFixed = isFixed;
            Strategy = TargetingSettings.Mode; // New towers start with the default (global) strategy.
            grid = gridManager != null ? gridManager : GridManager.Instance;
            if (grid != null) grid.ProjectionChanged += SnapViewToGrid;
            name = $"{data.displayName} ({cell.x},{cell.y})";
            if (body == null) body = GetComponentInChildren<SpriteRenderer>();
            if (body != null) body.color = data.color;
            if (look != null)
            {
                look.SetTint(data.color); // Top-down tint; a skinned sprite is drawn untinted.
                look.SetIsometricSlot(isFixed ? data.fixedBodySlot : data.bodySlot);
            }
            if (shadowSkin != null) shadowSkin.SetSlot(data.shadowSlot);
            transform.localScale *= data.topDownScale; // A skinned sprite cancels this: native pixel size.

            // The kick scales only the body (the square in top-down; the pillar and cap in
            // isometric), never the base plate or the ground shadow.
            baseScale = KickTarget.localScale;
            ApplyView();
            // Random first delay spreads the scans of many towers over different frames.
            scanTimer = Random.Range(0f, scanInterval);
        }

        private void OnEnable()
        {
            TargetingSettings.ModeChanged += OnModeChanged;
        }

        private void OnDisable()
        {
            TargetingSettings.ModeChanged -= OnModeChanged;
        }

        private void OnDestroy()
        {
            if (grid != null) grid.ProjectionChanged -= SnapViewToGrid;
        }

        // Visual only: after a projection switch, draw the tower on its cell again.
        private void SnapViewToGrid()
        {
            if (grid == null) return;
            transform.position = grid.CellToWorld(Cell);
            ApplyView();
        }

        // Visual only: sprite for the current projection and (isometric) draw order by depth.
        private void ApplyView()
        {
            if (look != null) look.Apply(grid);
            if (fixedMark != null) fixedMark.SetActive(IsFixed && grid != null && grid.ProjectionType != GridProjectionType.Isometric);
            // The look may change the body's scale (native pixel size when skinned), so the kick
            // always starts from the body's current rest scale.
            kickTimeLeft = 0f;
            baseScale = KickTarget.localScale;
            if (depth != null) depth.Initialize(grid, Cell);
        }

        // T or the HUD arrows changed the global mode: every tower follows it.
        private void OnModeChanged(TargetingMode mode)
        {
            SetStrategy(mode);
        }

        // The one method that changes a tower's strategy (global mode, arrow keys, tower card).
        // The new strategy picks the target at once; the fire cooldown and the scan rhythm are
        // left untouched, so a change never makes a tower fire sooner or later.
        public void SetStrategy(TargetingMode mode)
        {
            if (Strategy == mode) return;
            Strategy = mode;
            ScanForTarget();
            StrategyChanged?.Invoke(this);
        }

        private void Update()
        {
            if (Data == null) return;

            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer += scanInterval;
                ScanForTarget();
            }

            // Between scans, drop a target that died or was destroyed (no stale reference).
            if (CurrentTarget == null) currentTarget = null;

            fireCooldown = Mathf.Max(0f, fireCooldown - Time.deltaTime);
            TryFire();
            UpdateFireKick();
        }

        // Visual only: return to the normal size once the short fire "kick" is over.
        private void UpdateFireKick()
        {
            if (kickTimeLeft <= 0f) return;
            kickTimeLeft -= Time.unscaledDeltaTime;
            if (kickTimeLeft <= 0f) KickTarget.localScale = baseScale;
        }

        // Fires one projectile if the cooldown is over and the target is still alive.
        private void TryFire()
        {
            if (fireCooldown > 0f || projectilePrefab == null) return;
            Enemy target = CurrentTarget; // Re-checks alive/destroyed right before firing.
            if (target == null) return;

            Projectile projectile = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
            projectile.SetLook(Data);
            projectile.Initialize(target, Data.damage, Data.projectileSpeed, grid, Cell);
            fireCooldown = 1f / Data.fireRate;

            KickTarget.localScale = baseScale * Data.fireKickScale; // Visual only.
            kickTimeLeft = fireKickDuration;
            Fired?.Invoke(this);
        }

        // Collects enemies within range (manual squared-distance check against the registry)
        // and lets the current strategy pick one. Public so it can be triggered for testing.
        public void ScanForTarget()
        {
            if (Data == null) return;

            Vector2 position = Cell; // Grid coordinates; Data.range is in cells.
            float rangeSqr = Data.range * Data.range;
            IReadOnlyList<Enemy> enemies = EnemyRegistry.Active;

            inRange.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || enemy.IsDead || enemy.Data == null) continue;
                if ((enemy.GridPosition - position).sqrMagnitude <= rangeSqr)
                    inRange.Add(enemy);
            }

            currentTarget = TargetSelectors.Get(Strategy).SelectTarget(position, inRange);
        }
    }
}
