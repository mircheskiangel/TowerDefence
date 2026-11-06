using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TowerDefense
{
    // The Win/Lose screen. Hidden while playing; when GameManager leaves Playing it shows
    // "YOU WIN!" or "GAME OVER" with a short result line and a Restart button (R also restarts).
    // A win also offers the next level, or the menu after the last level; a loss offers the menu.
    // This script sits on an always-active object; only the 'panel' child is shown/hidden.
    public class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private WaveManager waves;
        [SerializeField] private GameObject panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text detail;
        [SerializeField] private Button restartButton;
        [Tooltip("Next level after a win (the menu after the last level), the menu after a loss.")]
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextLabel;
        [SerializeField] private float buttonSpacing = 200f;

        private Vector2 restartHome;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            if (restartButton != null) restartHome = ((RectTransform)restartButton.transform).anchoredPosition;
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.StateChanged += HandleStateChanged;
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (nextButton != null) nextButton.onClick.AddListener(NextOrMenu);
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
            if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
            if (nextButton != null) nextButton.onClick.RemoveListener(NextOrMenu);
        }

        private void Update()
        {
            // Update still runs while Time.timeScale is 0, so the R key works on the paused screen.
            if (panel == null || !panel.activeSelf) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) Restart();
        }

        private void HandleStateChanged(GameState state)
        {
            if (panel == null) return;
            panel.SetActive(state != GameState.Playing);
            LayoutButtons(state != GameState.Playing && LevelManager.Instance != null);

            if (state == GameState.Won)
            {
                title.text = "YOU WIN!";
                detail.text = $"Lives left: {(resources != null ? resources.Lives : 0)}";
            }
            else if (state == GameState.Lost)
            {
                title.text = "GAME OVER";
                int wave = waves != null ? Mathf.Max(1, waves.CurrentWaveNumber) : 0;
                int total = waves != null ? waves.TotalWaves : 0;
                detail.text = $"Reached wave {wave} of {total}";
            }
        }

        // Restart and the second button side by side (Restart alone without levels).
        private void LayoutButtons(bool showNext)
        {
            if (nextButton != null) nextButton.gameObject.SetActive(showNext);
            if (restartButton != null)
                ((RectTransform)restartButton.transform).anchoredPosition = restartHome + (showNext ? Vector2.left * buttonSpacing : Vector2.zero);
            if (!showNext || nextButton == null) return;
            ((RectTransform)nextButton.transform).anchoredPosition = restartHome + Vector2.right * buttonSpacing;
            if (nextLabel != null) nextLabel.text = gameManager.State == GameState.Won && LevelManager.Instance.HasNextLevel ? "Next level" : "Menu";
        }

        private void Restart()
        {
            if (gameManager != null) gameManager.Restart();
        }

        private void NextOrMenu()
        {
            if (LevelManager.Instance == null) return;
            if (gameManager.State == GameState.Won) LevelManager.Instance.LoadNextOrMenu();
            else LevelManager.Instance.LoadMenu();
        }
    }
}
