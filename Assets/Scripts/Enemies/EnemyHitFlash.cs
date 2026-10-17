using UnityEngine;

namespace TowerDefense
{
    // Visual only: makes the enemy sprite flash white for a moment when it is hit.
    // A plain timer in Update (no coroutine), using unscaled time so a flash still ends
    // when the game is frozen. The base color is stored once, so being hit again during
    // a flash always returns to the exact original color.
    public class EnemyHitFlash : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float duration = 0.08f;

        private Color baseColor = Color.white;
        private float timeLeft;

        // Called by Enemy.Initialize after it has applied the EnemySO color.
        public void SetBaseColor(Color color)
        {
            baseColor = color;
            timeLeft = 0f;
            if (body != null) body.color = baseColor;
        }

        public void Flash()
        {
            if (body == null) return;
            body.color = flashColor;
            timeLeft = duration; // Restarting the timer never changes the stored base color.
        }

        private void Update()
        {
            if (timeLeft <= 0f || body == null) return;
            timeLeft -= Time.unscaledDeltaTime;
            if (timeLeft <= 0f) body.color = baseColor;
        }
    }
}
