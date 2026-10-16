using TMPro;
using UnityEngine;

namespace TowerDefense
{
    // Shows the wave status: "Wave 2/5 starts in 7s (Space to skip)", "Wave 2/5",
    // "All waves cleared" or "Defeated". The text is only rebuilt when what it shows actually changes
    // (phase, wave number or whole seconds), so no new string is allocated every frame.
    public class WaveHUD : MonoBehaviour
    {
        private enum Phase { None, Countdown, Wave, AllCleared, Defeated }

        [SerializeField] private WaveManager waves;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text label;

        private Phase shownPhase = Phase.None;
        private int shownWave = -1;
        private int shownSeconds = -1;

        private void Update()
        {
            if (waves == null || label == null) return;

            Phase phase = gameManager != null && gameManager.State == GameState.Lost ? Phase.Defeated
                        : waves.AllCleared ? Phase.AllCleared
                        : waves.IsCountingDown ? Phase.Countdown
                        : waves.CurrentWaveNumber > 0 ? Phase.Wave
                        : Phase.None;
            int wave = phase == Phase.Countdown ? waves.CurrentWaveNumber + 1 : waves.CurrentWaveNumber;
            int seconds = phase == Phase.Countdown ? Mathf.CeilToInt(waves.CountdownRemaining) : 0;

            if (phase == shownPhase && wave == shownWave && seconds == shownSeconds) return;
            shownPhase = phase;
            shownWave = wave;
            shownSeconds = seconds;

            switch (phase)
            {
                case Phase.Countdown:
                    label.text = $"Wave {wave}/{waves.TotalWaves} starts in {seconds}s (Space to skip)";
                    break;
                case Phase.Wave:
                    label.text = $"Wave {wave}/{waves.TotalWaves}";
                    break;
                case Phase.AllCleared:
                    label.text = "All waves cleared";
                    break;
                case Phase.Defeated:
                    label.text = "Defeated";
                    break;
                default:
                    label.text = "";
                    break;
            }
        }
    }
}
