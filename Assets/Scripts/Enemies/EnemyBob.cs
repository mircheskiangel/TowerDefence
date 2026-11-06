using UnityEngine;

namespace TowerDefense
{
    // Visual only: a slight vertical bob of the enemy's ball while it walks (isometric view, Balls
    // style; characters have the bob in their walk frames, see EnemyWalkAnimator).
    // It moves only the visual child ('ball'); the enemy's position, its contact shadow, its
    // health bar and its draw order are untouched. The bob follows the distance walked, so an
    // enemy that stands still (waiting for a path) settles back onto the ground. In top-down
    // there is no bob, so that view stays exactly as before.
    public class EnemyBob : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private Transform ball;
        [Tooltip("Height of a hop in world units (1.5 px at 64 px per unit).")]
        [SerializeField] private float amplitude = 1.5f / 64f;
        [Tooltip("Hops per cell walked.")]
        [SerializeField] private float hopsPerCell = 2f;
        [Tooltip("How fast the ball settles when the enemy stops (per second).")]
        [SerializeField] private float settleSpeed = 8f;

        private Vector2 lastPosition;
        private bool hasLast;
        private float walked;
        private float height;

        private void LateUpdate()
        {
            if (enemy == null || ball == null) return;
            GridManager grid = GridManager.Instance;
            bool iso = grid != null && grid.ProjectionType == GridProjectionType.Isometric;

            Vector2 position = enemy.GridPosition;
            float step = hasLast ? (position - lastPosition).magnitude : 0f;
            lastPosition = position;
            hasLast = true;

            if (!iso || EnemySkinSettings.Style != EnemySkinStyle.Balls)
            {
                height = 0f;
                ball.localPosition = Vector3.zero;
                return;
            }

            if (step > 0f)
            {
                walked += step;
                height = amplitude * Mathf.Abs(Mathf.Sin(walked * hopsPerCell * Mathf.PI));
            }
            else
            {
                height = Mathf.MoveTowards(height, 0f, settleSpeed * amplitude * Time.deltaTime);
            }
            float parentScale = ball.parent != null ? ball.parent.lossyScale.y : 1f;
            ball.localPosition = new Vector3(0f, height / (Mathf.Approximately(parentScale, 0f) ? 1f : parentScale), 0f);
        }
    }
}
