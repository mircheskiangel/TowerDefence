using TMPro;
using UnityEngine;

namespace TowerDefense
{
    // The large health bar of a boss at the top centre of the screen, in both views (visible only
    // while a boss is alive), and the announcements of the final wave and of the boss.
    // It sits on an always-active object; only the 'root' child is shown or hidden.
    public class BossBar : MonoBehaviour
    {
        private const float MessageSeconds = 3f;

        [SerializeField] private GridManager grid;
        [SerializeField] private WaveManager waves;
        [SerializeField] private MessageDisplay messages;
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text label;
        [SerializeField] private Vector2 isoPosition = new Vector2(0f, -112f);
        [SerializeField] private Vector2 isoSize = new Vector2(640f, 58f);
        [SerializeField] private Vector2 topDownPosition = new Vector2(0f, -72f);
        [SerializeField] private Vector2 topDownSize = new Vector2(720f, 36f);
        [SerializeField] private float inset = 8f;

        private Enemy boss;
        private string bossName;

        private void OnEnable()
        {
            if (grid != null) grid.ProjectionChanged += Layout;
            if (waves != null)
            {
                waves.EnemySpawned += HandleEnemySpawned;
                waves.WaveStarted += HandleWaveStarted;
            }
        }

        private void OnDisable()
        {
            if (grid != null) grid.ProjectionChanged -= Layout;
            if (waves != null)
            {
                waves.EnemySpawned -= HandleEnemySpawned;
                waves.WaveStarted -= HandleWaveStarted;
            }
            if (boss != null) boss.HealthChanged -= HandleHealthChanged;
        }

        private void Start()
        {
            root.gameObject.SetActive(false);
            Layout();
        }

        private void HandleWaveStarted(int wave)
        {
            string text = LevelManager.Instance != null ? LevelManager.Instance.Current.finalWaveMessage : "";
            if (wave == waves.TotalWaves && !string.IsNullOrEmpty(text)) messages.Show(text, MessageSeconds);
        }

        private void HandleEnemySpawned(Enemy enemy)
        {
            if (!enemy.Data.isBoss) return;
            boss = enemy;
            bossName = enemy.Data.displayName.ToUpperInvariant();
            boss.HealthChanged += HandleHealthChanged;
            root.gameObject.SetActive(true);
            Layout();
            HandleHealthChanged(boss.CurrentHealth, boss.MaxHealth);
            messages.Show($"THE {bossName} APPROACHES", MessageSeconds);
        }

        private void Update()
        {
            if (root.gameObject.activeSelf && (boss == null || boss.IsDead)) root.gameObject.SetActive(false);
        }

        private void Layout()
        {
            bool isometric = grid.ProjectionType == GridProjectionType.Isometric;
            root.anchoredPosition = isometric ? isoPosition : topDownPosition;
            root.sizeDelta = isometric ? isoSize : topDownSize;
            label.fontSize = isometric ? 28f : 22f;
            if (boss != null) HandleHealthChanged(boss.CurrentHealth, boss.MaxHealth);
        }

        private void HandleHealthChanged(float current, float max)
        {
            float width = Mathf.Max(0f, root.sizeDelta.x - 2f * inset) * (max > 0f ? Mathf.Clamp01(current / max) : 0f);
            fill.sizeDelta = new Vector2(width, Mathf.Max(0f, root.sizeDelta.y - 2f * inset));
            label.text = $"{bossName}   {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }
    }
}
