using UnityEngine;

namespace TowerDefense
{
    // Visual only: the walk animation of an enemy character (isometric, Characters style).
    // It changes only the visual child's sprite and flip, never the enemy's transform.
    // - Frames follow the distance walked, so the rate scales with the enemy's current speed
    //   and an enemy that stands still stops animating. At its normal speed one frame lasts
    //   EnemySO.walkFrameSeconds (0.18 s Grunt, 0.30 s Tank).
    // - The sprite is mirrored when the enemy moves LEFT on screen. That comes from the projected
    //   movement, not the grid direction: in isometric both +cy and -cx move left. A move with no
    //   horizontal part on screen keeps the current facing.
    public class EnemyWalkAnimator : MonoBehaviour
    {
        private const float FacingEpsilon = 0.00001f;

        [SerializeField] private Enemy enemy;
        [SerializeField] private ProjectionLook look;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SkinSet skin;

        private Vector2 lastPosition;
        private bool hasLast;
        private float walked;
        private bool facingLeft;

        private void LateUpdate()
        {
            if (enemy == null || body == null || look == null || enemy.Data == null) return;
            GridManager grid = GridManager.Instance;
            if (grid == null) return;

            Vector2 position = enemy.GridPosition;
            float step = hasLast ? (position - lastPosition).magnitude : 0f;
            float screenDx = hasLast ? grid.GridToWorld(position).x - grid.GridToWorld(lastPosition).x : 0f;
            lastPosition = position;
            hasLast = true;

            walked += step;
            if (screenDx < -FacingEpsilon) facingLeft = true;
            else if (screenDx > FacingEpsilon) facingLeft = false;

            bool animate = EnemySkinSettings.Style == EnemySkinStyle.Characters && look.IsSkinned
                           && grid.ProjectionType == GridProjectionType.Isometric && skin != null;
            if (!animate)
            {
                if (body.flipX) body.flipX = false;
                return;
            }

            SkinSlot slot = enemy.WalkSlot;
            int frames = skin.VariantCount(slot);
            if (frames > 0)
            {
                float cellsPerFrame = Mathf.Max(0.01f, enemy.Data.walkFrameSeconds * enemy.Data.moveSpeed);
                int frame = (int)(walked / cellsPerFrame) % frames;
                Sprite sprite = skin.Get(slot, frame);
                if (body.sprite != sprite) body.sprite = sprite;
            }
            if (body.flipX != facingLeft) body.flipX = facingLeft;
        }
    }
}
