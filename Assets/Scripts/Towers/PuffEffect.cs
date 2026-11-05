using UnityEngine;

namespace TowerDefense
{
    // Visual only: a tiny dust puff when a tower is placed (isometric skin). A handful of soft
    // particles drift outwards along the ground plane (2:1), rise a little and fade out, then the
    // whole object removes itself. Directions come from a seed, so a puff is always the same.
    public class PuffEffect : MonoBehaviour
    {
        private const int Count = 8;
        private const float Duration = 0.45f;

        private Transform[] particles;
        private SpriteRenderer[] renderers;
        private Vector3[] velocities;
        private float elapsed;

        public static void Spawn(Sprite sprite, Material material, Vector3 position, int sortingOrder, int seed)
        {
            GameObject root = new GameObject("PlacementPuff");
            root.transform.position = position;
            PuffEffect puff = root.AddComponent<PuffEffect>();
            puff.particles = new Transform[Count];
            puff.renderers = new SpriteRenderer[Count];
            puff.velocities = new Vector3[Count];
            var rng = new System.Random(seed);
            for (int i = 0; i < Count; i++)
            {
                float a = (i + (float)rng.NextDouble() * 0.6f) * Mathf.PI * 2f / Count;
                float speed = 0.55f + (float)rng.NextDouble() * 0.35f;
                GameObject go = new GameObject("Dust");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.08f, Mathf.Sin(a) * 0.04f, 0f);
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (material != null) sr.sharedMaterial = material;
                sr.sortingOrder = sortingOrder;
                sr.color = new Color(1f, 1f, 1f, 0.85f);
                puff.particles[i] = go.transform;
                puff.renderers[i] = sr;
                puff.velocities[i] = new Vector3(Mathf.Cos(a) * speed, Mathf.Sin(a) * speed * 0.5f + 0.15f, 0f);
            }
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Duration);
            float slow = 1f - t * 0.7f; // particles slow down as they spread
            for (int i = 0; i < Count; i++)
            {
                particles[i].localPosition += velocities[i] * slow * Time.deltaTime;
                renderers[i].color = new Color(1f, 1f, 1f, 0.85f * (1f - t));
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
