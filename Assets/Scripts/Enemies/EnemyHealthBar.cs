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
        [Tooltip("The enemy's body sprite: the bar sits just above its top edge.")]
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer background;
        [SerializeField] private SpriteRenderer fill;
        [Tooltip("Bar size in world units (does not depend on the enemy's scale).")]
        [SerializeField] private Vector2 size = new Vector2(0.6f, 0.09f);
        [Tooltip("Gap between the top of the enemy sprite and the bar, in world units.")]
        [SerializeField] private float gapAboveEnemy = 0.08f;
        [SerializeField] private Color fullColor = new Color(0.25f, 0.9f, 0.3f);
        [SerializeField] private Color halfColor = new Color(1f, 0.85f, 0.2f);
        [SerializeField] private Color lowColor = new Color(0.95f, 0.25f, 0.2f);
        [Header("Isometric skin (pixel bar)")]
        [Tooltip("Tells whether the body is drawn with a skin sprite (then the pixel bar is used).")]
        [SerializeField] private ProjectionLook look;
        [SerializeField] private Color skinnedBorderColor = new Color(20f / 255f, 14f / 255f, 14f / 255f);
        [SerializeField] private Color skinnedFillColor = new Color(210f / 255f, 40f / 255f, 40f / 255f);
        [SerializeField] private float pixelsPerUnit = 64f;

        private int skinnedWidthPixels = 18;
        private float fraction = 1f;
        private bool Skinned => look != null && look.IsSkinned;
        // Pixel bar: fill W x 2 px inside a 1 px dark border, 3 px above the ball.
        private Vector2 BarSize => Skinned ? new Vector2((skinnedWidthPixels + 2) / pixelsPerUnit, 4f / pixelsPerUnit) : size;
        private Vector2 FillSize => Skinned ? new Vector2(skinnedWidthPixels / pixelsPerUnit, 2f / pixelsPerUnit) : size;
        private float Gap => Skinned ? 3f / pixelsPerUnit : gapAboveEnemy;

        // Fill width of the skinned (isometric) bar in pixels: narrower for a Grunt than a Tank.
        public void SetSkinnedWidth(int pixels)
        {
            skinnedWidthPixels = Mathf.Max(4, pixels);
        }

        private Color backgroundColor;
        private bool backgroundCaptured;

        private void Awake()
        {
            CaptureBackground();
        }

        private void CaptureBackground()
        {
            if (backgroundCaptured || background == null) return;
            backgroundColor = background.color;
            backgroundCaptured = true;
        }

        private void OnEnable()
        {
            if (enemy != null) enemy.HealthChanged += HandleHealthChanged;
        }

        private void OnDisable()
        {
            if (enemy != null) enemy.HealthChanged -= HandleHealthChanged;
        }

        // Called by Enemy.Initialize once its scale is known: lays the bar out and hides it.
        public void ResetBar()
        {
            CaptureBackground();
            Layout();
            SetFill(1f);
            SetVisible(false);
        }

        // Cancels the parent's scale so the bar keeps the same world size on a small Grunt and a
        // big Tank, and puts it just above the top of the body sprite (a centered circle in
        // top-down, a standing circle in isometric). Keeps the current fill and visibility.
        public void Layout()
        {
            Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float sx = Mathf.Approximately(parentScale.x, 0f) ? 1f : parentScale.x;
            float sy = Mathf.Approximately(parentScale.y, 0f) ? 1f : parentScale.y;

            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(1f / sx, 1f / sy, 1f);
            // Top of the body sprite in the enemy's local space (the body may be a scaled child;
            // its bob offset is ignored, so the bar stays still while the ball bobs).
            float top = 0.5f;
            if (body != null && body.sprite != null)
                top = body.sprite.bounds.max.y * (body.transform == transform.parent ? 1f : body.transform.localScale.y);
            Vector2 bar = BarSize;
            transform.localPosition = new Vector3(0f, top + (Gap + bar.y * 0.5f) / sy, 0f);

            background.transform.localScale = new Vector3(bar.x, bar.y, 1f);
            background.transform.localPosition = Vector3.zero;
            SetFill(fraction); // Bar style may have changed with the projection.
        }

        private void HandleHealthChanged(float current, float max)
        {
            float f = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            SetFill(f);
            SetVisible(f < 1f);
        }

        // Shrinks the fill from the right: the square sprite is centered, so it is moved left
        // by half of the missing width to keep its left edge in place.
        private void SetFill(float newFraction)
        {
            fraction = newFraction;
            Vector2 full = FillSize;
            float width = full.x * fraction;
            fill.transform.localScale = new Vector3(width, full.y, 1f);
            fill.transform.localPosition = new Vector3(-(full.x - width) * 0.5f, 0f, 0f);
            if (Skinned)
            {
                fill.color = skinnedFillColor;
                background.color = skinnedBorderColor;
                return;
            }
            background.color = backgroundColor;
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
