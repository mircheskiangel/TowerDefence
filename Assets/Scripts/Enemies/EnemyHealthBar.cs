using UnityEngine;

namespace TowerDefense
{
    // Visual only: a small world-space health bar (dark background + colored fill) above an enemy.
    // Hidden at full health, shown after the first hit. It is redrawn only when the enemy's
    // health changes (Enemy.HealthChanged event), never every frame. It is a child of the
    // enemy, so it is destroyed together with it.
    public class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private Enemy enemy;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private SpriteRenderer fill;
        [Tooltip("Bar size in world units (does not depend on the enemy's scale).")]
        [SerializeField] private Vector2 size = new Vector2(0.6f, 0.09f);
        [Tooltip("Gap between the top of the enemy sprite and the bar, in world units.")]
        [SerializeField] private float gapAboveEnemy = 0.08f;
        [SerializeField] private Color fullColor = new Color(0.25f, 0.9f, 0.3f);
        [SerializeField] private Color halfColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color lowColor = new Color(0.95f, 0.25f, 0.2f);

        private void OnEnable()
        {
            if (enemy != null) enemy.HealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.HealthChanged -= HandleHealthChanged;
        }

        // Called by Enemy.Initialize once its scale is known: cancels the parent's scale so the
        // bar keeps the same world size on a small Grunt and a big Tank, and hides the bar.
        public void ResetBar()
        {
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
            float sy = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;

            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);
            // The enemy sprite is 1 local unit tall, so its top edge is at local y = 0.5.
            transform.localPosition = new Vector3(0f, 0.5f + (gapAboveEnemy + size.y * 0.5f) / sy, 0f);

            background.transform.localScale = new Vector3(size.x, size.y, 1f);
            background.transform.localPosition = Vector3.zero;
            SetFill(1f);
            SetVisible(false);
        }

        private void HandleHealthChanged(float current, float max)
        {
            float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            SetFill(fraction);
            SetVisible(fraction < 1f);
        }

        // Shrinks the fill from the right: the square sprite is centered, so it is moved left
        // by half of the missing width to keep its left edge in place.
        private void SetFill(float fraction)
        {
            float width = size.x * fraction;
            fill.transform.localScale = new Vector3(width, size.y, 1f);
            fill.transform.localPosition = new Vector3(-(size.x - width) * 0.5f, 0f, 0f);
            fill.color = fraction > 0.5f
                ? Color.Lerp(halfColor, fullColor, (fraction - 0.5f) * 2f)
                : Color.Lerp(lowColor, halfColor, fraction * 2f);
        }

        private void SetVisible(bool visible)
        {
            background.enabled = visible;
            fill.enabled = visible;
        }
    }
}
