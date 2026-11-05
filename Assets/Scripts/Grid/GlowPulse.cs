using UnityEngine;

namespace TowerDefense
{
    // Visual only: a soft, slow pulse of a glow sprite's opacity (the spawn portal's halo).
    // Uses unscaled time, so it keeps breathing on the paused end screen.
    public class GlowPulse : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private float minAlpha = 0.45f;
        [SerializeField] private float maxAlpha = 1f;
        [Tooltip("Seconds for one full pulse.")]
        [SerializeField] private float period = 1.8f;

        public void Configure(SpriteRenderer renderer)
        {
            target = renderer;
        }

        private void Update()
        {
            if (target == null) return;
            float t = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / period);
            Color c = target.color;
            c.a = Mathf.Lerp(minAlpha, maxAlpha, t);
            target.color = c;
        }
    }
}
