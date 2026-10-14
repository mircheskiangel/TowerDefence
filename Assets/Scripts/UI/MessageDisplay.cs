using TMPro;
using UnityEngine;

namespace TowerDefense
{
    // Shows a short on-screen message (e.g. "Path blocked!") that stays for a moment
    // and then fades out. Calling Show again restarts the timer.
    public class MessageDisplay : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private float defaultDuration = 1.5f;
        [Tooltip("Seconds at the end of the duration during which the text fades out.")]
        [SerializeField] private float fadeTime = 0.5f;

        private float timeLeft;

        private void Awake()
        {
            if (label == null) label = GetComponent<TMP_Text>();
            if (label != null) label.alpha = 0f;
        }

        public void Show(string message)
        {
            Show(message, defaultDuration);
        }

        public void Show(string message, float duration)
        {
            if (label == null) return;
            label.text = message;
            timeLeft = duration;
            label.alpha = 1f;
        }

        private void Update()
        {
            if (label == null || timeLeft <= 0f) return;
            timeLeft -= Time.deltaTime;
            label.alpha = Mathf.Clamp01(timeLeft / Mathf.Max(fadeTime, 0.01f));
        }
    }
}
