using UnityEngine;

namespace TowerDefense
{
    // Visual only: a short fade-out of copies of some sprites (an enemy's ball and shadow when it
    // dies). The copies live on their own object, so the enemy itself is destroyed exactly as
    // before and no gameplay code ever sees them. They grow a little and fade to transparent.
    public class VisualFade : MonoBehaviour
    {
        private SpriteRenderer[] parts;
        private Color[] startColors;
        private Vector3[] startScales;
        private float duration;
        private float grow;
        private float elapsed;

        // Copies the given renderers (sprite, colour, order, world position and scale) and fades them.
        public static void Spawn(float duration, float grow, params SpriteRenderer[] sources)
        {
            GameObject root = new GameObject("DeathFade");
            VisualFade fade = root.AddComponent<VisualFade>();
            var copies = new System.Collections.Generic.List<SpriteRenderer>();
            foreach (SpriteRenderer src in sources)
            {
                if (src == null || !src.enabled || !src.gameObject.activeInHierarchy || src.sprite == null) continue;
                GameObject go = new GameObject(src.name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = src.transform.position;
                go.transform.localScale = src.transform.lossyScale;
                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = src.sprite; // The current walk frame...
                sr.flipX = src.flipX;   // ...and the current facing.
                sr.sharedMaterial = src.sharedMaterial;
                sr.color = src.color;
                sr.sortingOrder = src.sortingOrder;
                copies.Add(sr);
            }
            fade.parts = copies.ToArray();
            fade.startColors = new Color[fade.parts.Length];
            fade.startScales = new Vector3[fade.parts.Length];
            for (int i = 0; i < fade.parts.Length; i++)
            {
                fade.startColors[i] = fade.parts[i].color;
                fade.startScales[i] = fade.parts[i].transform.localScale;
            }
            fade.duration = Mathf.Max(0.01f, duration);
            fade.grow = grow;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < parts.Length; i++)
            {
                Color c = startColors[i];
                c.a *= 1f - t;
                parts[i].color = c;
                parts[i].transform.localScale = startScales[i] * Mathf.Lerp(1f, grow, t);
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
