using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Shared logic for all selectors: score every enemy, keep the highest score,
    // and break ties by the smaller distance to the tower (so the result is deterministic).
    // Subclasses only define what "best" means by overriding Score.
    public abstract class ScoredTargetSelector : ITargetSelector
    {
        private const float TieEpsilon = 0.0001f;

        // Higher score = better target.
        protected abstract float Score(Enemy enemy, float sqrDistance);

        public Enemy SelectTarget(Vector2 towerPosition, IReadOnlyList<Enemy> inRange)
        {
            Enemy best = null;
            float bestScore = float.NegativeInfinity;
            float bestSqrDistance = float.PositiveInfinity;

            for (int i = 0; i < inRange.Count; i++)
            {
                Enemy enemy = inRange[i];
                if (enemy == null || enemy.IsDead) continue;

                float sqrDistance = ((Vector2)enemy.transform.position - towerPosition).sqrMagnitude;
                float score = Score(enemy, sqrDistance);

                bool better = score > bestScore + TieEpsilon;
                bool tieButCloser = Mathf.Abs(score - bestScore) <= TieEpsilon && sqrDistance < bestSqrDistance;
                if (best == null || better || tieButCloser)
                {
                    best = enemy;
                    bestScore = score;
                    bestSqrDistance = sqrDistance;
                }
            }
            return best;
        }
    }

    // First: the enemy furthest along its path (closest to the goal).
    public class FirstSelector : ScoredTargetSelector
    {
        protected override float Score(Enemy enemy, float sqrDistance) => enemy.PathProgress;
    }

    // Closest: the enemy nearest to the tower.
    public class ClosestSelector : ScoredTargetSelector
    {
        protected override float Score(Enemy enemy, float sqrDistance) => -sqrDistance;
    }

    // Strongest: the enemy with the most health left.
    public class StrongestSelector : ScoredTargetSelector
    {
        protected override float Score(Enemy enemy, float sqrDistance) => enemy.CurrentHealth;
    }

    // Weakest: the enemy with the least health left (finish it off).
    public class WeakestSelector : ScoredTargetSelector
    {
        protected override float Score(Enemy enemy, float sqrDistance) => -enemy.CurrentHealth;
    }

    // Maps a TargetingMode to its selector. Selectors hold no state, so one shared
    // instance per mode is reused by every tower: no allocations while the game runs.
    public static class TargetSelectors
    {
        private static readonly ITargetSelector First = new FirstSelector();
        private static readonly ITargetSelector Closest = new ClosestSelector();
        private static readonly ITargetSelector Strongest = new StrongestSelector();
        private static readonly ITargetSelector Weakest = new WeakestSelector();

        public static ITargetSelector Get(TargetingMode mode)
        {
            switch (mode)
            {
                case TargetingMode.Closest: return Closest;
                case TargetingMode.Strongest: return Strongest;
                case TargetingMode.Weakest: return Weakest;
                default: return First;
            }
        }
    }
}
