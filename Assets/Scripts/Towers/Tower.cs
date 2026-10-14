using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // A placed tower. Every scanInterval seconds it collects the enemies within its range
    // and asks the selector for the current global TargetingMode which one to target.
    // A debug line is drawn to the current target (shooting is added in M4 part 2).
    public class Tower : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float scanInterval = 0.1f;
        [SerializeField] private Color targetLineColor = new Color(1f, 0.3f, 0.3f, 0.9f);
        [SerializeField] private float targetLineWidth = 0.05f;
        [SerializeField] private int targetLineSortingOrder = 18;

        private readonly List<Enemy> inRange = new List<Enemy>(); // Reused every scan: no garbage.
        private Enemy currentTarget;
        private float scanTimer;
        private LineRenderer targetLine;

        public TowerSO Data { get; private set; }
        public Vector2Int Cell { get; private set; }

        // The enemy this tower is aiming at, or null. Unity's overloaded == makes a destroyed
        // enemy compare equal to null, so a dead target is never returned.
        public Enemy CurrentTarget => currentTarget != null && !currentTarget.IsDead ? currentTarget : null;

        public void Initialize(TowerSO data, Vector2Int cell)
        {
            Data = data;
            Cell = cell;
            name = $"{data.displayName} ({cell.x},{cell.y})";
            if (body == null) body = GetComponent<SpriteRenderer>();
            if (body != null) body.color = data.color;

            targetLine = CreateTargetLine();
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

        private void OnModeChanged(TargetingMode mode)
        {
            scanTimer = 0f; // Re-target immediately when the player presses T.
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

            UpdateTargetLine();
        }

        // Collects enemies within range (manual squared-distance check against the registry)
        // and lets the current strategy pick one. Public so it can be triggered for testing.
        public void ScanForTarget()
        {
            if (Data == null) return;

            Vector2 position = transform.position;
            float rangeSqr = Data.range * Data.range;
            IReadOnlyList<Enemy> enemies = EnemyRegistry.Active;

            inRange.Clear();
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];
                if (enemy == null || enemy.IsDead || enemy.Data == null) continue;
                if (((Vector2)enemy.transform.position - position).sqrMagnitude <= rangeSqr)
                    inRange.Add(enemy);
            }

            currentTarget = TargetSelectors.Get(TargetingSettings.Mode).SelectTarget(position, inRange);
        }

        private void UpdateTargetLine()
        {
            if (targetLine == null) return;
            Enemy target = CurrentTarget;
            targetLine.enabled = target != null;
            if (target == null) return;
            targetLine.SetPosition(0, transform.position);
            targetLine.SetPosition(1, target.transform.position);
        }

        // Debug visual: a thin line from the tower to its current target.
        private LineRenderer CreateTargetLine()
        {
            GameObject go = new GameObject("TargetLine");
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.sharedMaterial = lineMaterial;
            lr.startColor = targetLineColor;
            lr.endColor = targetLineColor;
            lr.startWidth = targetLineWidth;
            lr.endWidth = targetLineWidth;
            lr.sortingOrder = targetLineSortingOrder;
            lr.enabled = false;
            return lr;
        }
    }
}
