using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TowerDefense
{
    // The Win/Lose screen. Hidden while playing; when GameManager leaves Playing it shows
    // "YOU WIN!" or "GAME OVER" with a short result line and a Restart button (R also restarts).
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

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.StateChanged += HandleStateChanged;
            if (restartButton != null) restartButton.onClick.AddListener(Restart);
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
            if (restartButton != null) restartButton.onClick.RemoveListener(Restart);
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

        private void Restart()
        {
            if (gameManager != null) gameManager.Restart();
        }
    }
}
