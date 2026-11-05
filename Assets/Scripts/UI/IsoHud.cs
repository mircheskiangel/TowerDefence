using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    // Visual only: the isometric HUD in the reference style (dark metal-framed panels).
    // Top row: gold (coin icon), lives (heart icon), "WAVE 1 / 5", and the targeting selector
    // "TARGET: FIRST" with left/right arrow buttons. Bottom left: the tower card (icon and price).
    // Bottom centre: one message panel that shows a message (MessageDisplay.Shown, e.g. "Path
    // blocked!") for its duration, otherwise the wave countdown.
    // It sits on the always-active Canvas and switches between this HUD and the original top-down
    // labels on every projection change. The panels are used only in isometric AND when the skin
    // has the HUD panel sprite; otherwise the top-down HUD stays (an empty skin changes nothing).
    // Texts are rebuilt only when what they show changes (no new strings every frame).
    public class IsoHud : MonoBehaviour
    {
        private enum Phase { None, Countdown, Wave, AllCleared, Defeated }

        [SerializeField] private GridManager grid;
        [SerializeField] private SkinSet skin;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private WaveManager waves;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MessageDisplay messages;
        [Header("Roots")]
        [SerializeField] private GameObject isoRoot;
        [Tooltip("The original HUD labels, hidden while the isometric HUD is shown.")]
        [SerializeField] private GameObject[] topDownOnly = new GameObject[0];
        [Tooltip("Extra parts shown only with the isometric HUD (e.g. the Game Over frame).")]
        [SerializeField] private GameObject[] isoOnly = new GameObject[0];
        [Header("Texts and buttons")]
        [SerializeField] private TMP_Text goldText;
        [SerializeField] private TMP_Text livesText;
        [SerializeField] private TMP_Text waveText;
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private Button previousTarget;
        [SerializeField] private Button nextTarget;
        [SerializeField] private CanvasGroup bottomPanel;
        [SerializeField] private TMP_Text bottomText;
        [SerializeField] private float messageFadeTime = 0.5f;

        private bool active;
        private int shownGold = int.MinValue, shownLives = int.MinValue;
        private Phase shownPhase = Phase.None;
        private int shownWave = -1, shownSeconds = -1;
        private string message;
        private float messageLeft;
        private bool showingMessage;

        private void OnEnable()
        {
            if (grid != null) grid.ProjectionChanged += Apply;
            if (resources != null) resources.Changed += RefreshResources;
            if (messages != null) messages.Shown += HandleMessage;
            TargetingSettings.ModeChanged += RefreshTarget;
            if (previousTarget != null) previousTarget.onClick.AddListener(PreviousMode);
            if (nextTarget != null) nextTarget.onClick.AddListener(NextMode);
        }

        private void OnDisable()
        {
            if (grid != null) grid.ProjectionChanged -= Apply;
            if (resources != null) resources.Changed -= RefreshResources;
            if (messages != null) messages.Shown -= HandleMessage;
            TargetingSettings.ModeChanged -= RefreshTarget;
            if (previousTarget != null) previousTarget.onClick.RemoveListener(PreviousMode);
            if (nextTarget != null) nextTarget.onClick.RemoveListener(NextMode);
        }

        private void Start()
        {
            Apply();
        }

        // Shows the isometric HUD or the original labels for the current projection.
        public void Apply()
        {
            active = grid != null && grid.ProjectionType == GridProjectionType.Isometric
                     && skin != null && skin.Has(SkinSlot.HudPanel);
            if (isoRoot != null) isoRoot.SetActive(active);
            foreach (GameObject go in topDownOnly) if (go != null) go.SetActive(!active);
            foreach (GameObject go in isoOnly) if (go != null) go.SetActive(active);
            if (!active) return;
            shownGold = shownLives = int.MinValue;
            shownPhase = Phase.None;
            shownWave = shownSeconds = -1;
            RefreshResources();
            RefreshTarget(TargetingSettings.Mode);
        }

        private void RefreshResources()
        {
            if (!active || resources == null) return;
            if (resources.Gold != shownGold && goldText != null) goldText.text = (shownGold = resources.Gold).ToString();
            if (resources.Lives != shownLives && livesText != null) livesText.text = (shownLives = resources.Lives).ToString();
        }

        private void RefreshTarget(TargetingMode mode)
        {
            if (active && targetText != null) targetText.text = "TARGET: " + mode.ToString().ToUpperInvariant();
        }

        private static void PreviousMode()
        {
            int count = System.Enum.GetValues(typeof(TargetingMode)).Length;
            TargetingSettings.SetMode((TargetingMode)(((int)TargetingSettings.Mode + count - 1) % count));
        }

        private static void NextMode()
        {
            TargetingSettings.Cycle();
        }

        private void HandleMessage(string text, float duration)
        {
            message = text;
            messageLeft = duration;
            showingMessage = false; // Rebuild the bottom text on the next Update.
        }

        private void Update()
        {
            if (!active || waves == null) return;

            Phase phase = gameManager != null && gameManager.State == GameState.Lost ? Phase.Defeated
                        : waves.AllCleared ? Phase.AllCleared
                        : waves.IsCountingDown ? Phase.Countdown
                        : waves.CurrentWaveNumber > 0 ? Phase.Wave
                        : Phase.None;
            int wave = phase == Phase.Countdown ? waves.CurrentWaveNumber + 1 : waves.CurrentWaveNumber;
            int seconds = phase == Phase.Countdown ? Mathf.CeilToInt(waves.CountdownRemaining) : 0;
            bool waveChanged = phase != shownPhase || wave != shownWave;
            bool secondsChanged = seconds != shownSeconds;
            if (waveChanged && waveText != null)
            {
                waveText.text = phase == Phase.AllCleared ? "ALL CLEAR"
                              : phase == Phase.Defeated ? "DEFEATED"
                              : $"WAVE {Mathf.Max(1, wave)} / {waves.TotalWaves}";
            }

            // Bottom panel: a message has priority for its duration (fading at the end),
            // otherwise the countdown, otherwise hidden.
            if (messageLeft > 0f)
            {
                messageLeft -= Time.unscaledDeltaTime;
                if (!showingMessage && bottomText != null) bottomText.text = message;
                showingMessage = true;
                if (bottomPanel != null) bottomPanel.alpha = Mathf.Clamp01(messageLeft / Mathf.Max(messageFadeTime, 0.01f));
            }
            else
            {
                bool wasMessage = showingMessage;
                showingMessage = false;
                if (phase == Phase.Countdown)
                {
                    if ((waveChanged || secondsChanged || wasMessage) && bottomText != null)
                        bottomText.text = $"WAVE {wave} STARTS IN {seconds}  -  SPACE TO SKIP";
                    if (bottomPanel != null) bottomPanel.alpha = 1f;
                }
                else if (bottomPanel != null) bottomPanel.alpha = 0f;
            }

            shownPhase = phase;
            shownWave = wave;
            shownSeconds = seconds;
        }
    }
}
