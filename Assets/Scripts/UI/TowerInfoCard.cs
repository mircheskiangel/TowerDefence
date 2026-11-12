using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace TowerDefense
{
    // The card of the selected placed tower (click a tower to select it), in both views: its name,
    // "FIXED" for a level's own tower, "TARGET: STRONGEST" with < > buttons and one button per
    // strategy, the active one highlighted. LEFT / RIGHT ARROW step the selected tower's strategy
    // through the one order in TargetingSettings (wrapping); with SHIFT every tower of the same type
    // gets it. The keys, the < > buttons and the strategy buttons all end in Apply, which calls
    // Tower.SetStrategy (the same method the global T / HUD arrows use). It sits on an always-active
    // object; the card itself is shown only while a tower is selected.
    public class TowerInfoCard : MonoBehaviour
    {
        // Isometric: the bottom right corner. Top-down: smaller, in the margin right of the board.
        private const float IsoMargin = 20f, TopDownMargin = 8f, BottomY = 16f, TopDownScale = 0.6f;

        [SerializeField] private GridManager grid;
        [SerializeField] private TowerPlacer placer;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private MessageDisplay messages;
        [SerializeField] private AudioSettingsPanel audioPanel;
        [Header("Card")]
        [SerializeField] private RectTransform card;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private GameObject fixedTag;
        [SerializeField] private TMP_Text targetLabel;
        [SerializeField] private Button previousButton;
        [SerializeField] private Button nextButton;
        [Tooltip("One button per strategy, in TargetingSettings order.")]
        [SerializeField] private Button[] modeButtons = new Button[0];
        [SerializeField] private Color activeButton = new Color(1f, 0.84f, 0.35f);
        [SerializeField] private Color idleButton = new Color(0.42f, 0.47f, 0.56f);
        [SerializeField] private Color activeText = new Color(0.12f, 0.10f, 0.08f);
        [SerializeField] private Color idleText = new Color(0.93f, 0.91f, 0.85f);

        private Tower shown;
        private string[] targetTexts;               // "TARGET: FIRST" ...: built once, no garbage per change.
        private UnityEngine.Events.UnityAction previousAction, nextAction;
        private UnityEngine.Events.UnityAction[] modeActions;

        private void Awake()
        {
            targetTexts = new string[TargetingSettings.Count];
            for (int i = 0; i < targetTexts.Length; i++)
                targetTexts[i] = "TARGET: " + TargetingSettings.At(i).ToString().ToUpperInvariant();
            previousAction = () => Step(-1, ShiftHeld());
            nextAction = () => Step(1, ShiftHeld());
            modeActions = new UnityEngine.Events.UnityAction[modeButtons.Length];
            for (int i = 0; i < modeButtons.Length; i++)
            {
                TargetingMode mode = TargetingSettings.At(i);
                modeActions[i] = () => Apply(mode, ShiftHeld());
            }
            if (card != null) card.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (placer != null) placer.PlacedSelectionChanged += Show;
            if (grid != null) grid.ProjectionChanged += Layout;
            if (previousButton != null) previousButton.onClick.AddListener(previousAction);
            if (nextButton != null) nextButton.onClick.AddListener(nextAction);
            for (int i = 0; i < modeButtons.Length; i++) modeButtons[i].onClick.AddListener(modeActions[i]);
        }

        private void OnDisable()
        {
            if (placer != null) placer.PlacedSelectionChanged -= Show;
            if (grid != null) grid.ProjectionChanged -= Layout;
            if (previousButton != null) previousButton.onClick.RemoveListener(previousAction);
            if (nextButton != null) nextButton.onClick.RemoveListener(nextAction);
            for (int i = 0; i < modeButtons.Length; i++) modeButtons[i].onClick.RemoveListener(modeActions[i]);
            Watch(null);
        }

        private void Start()
        {
            Layout();
            Show(placer != null ? placer.SelectedPlaced : null);
        }

        // Arrow keys: one change per press (wasPressedThisFrame), so holding a key changes once.
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            int direction = (keyboard.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keyboard.leftArrowKey.wasPressedThisFrame ? 1 : 0);
            if (direction == 0 || !KeysAllowed) return;
            if (Step(direction, ShiftHeld())) AudioManager.Play(SfxId.UiClick); // Buttons click by themselves.
        }

        // Only while a level is being played: not on the level select, not after a win or a loss,
        // not while the audio panel is open.
        public bool KeysAllowed =>
            LevelManager.Instance != null && !LevelManager.Instance.InMenu
            && (gameManager == null || gameManager.IsPlaying)
            && (audioPanel == null || !audioPanel.IsOpen);

        private static bool ShiftHeld()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.shiftKey.isPressed;
        }

        // The arrow keys and the < > buttons: the next (+1) or previous (-1) strategy of the selected tower.
        public bool Step(int direction, bool allOfType)
        {
            Tower tower = placer != null ? placer.SelectedPlaced : null;
            if (tower == null)
            {
                Hint();
                return false;
            }
            return Apply(TargetingSettings.Step(tower.Strategy, direction), allOfType);
        }

        // Every change ends here: the selected tower (or all of its type) gets 'mode'.
        public bool Apply(TargetingMode mode, bool allOfType)
        {
            Tower tower = placer != null ? placer.SelectedPlaced : null;
            if (tower == null)
            {
                Hint();
                return false;
            }
            if (allOfType)
            {
                placer.SetStrategyForType(tower.Data, mode);
                if (messages != null) messages.Show($"All {tower.Data.displayName}s: {mode}");
            }
            else
            {
                tower.SetStrategy(mode);
                if (messages != null) messages.Show($"{tower.Data.displayName}: {mode}");
            }
            return true;
        }

        private void Hint()
        {
            if (messages != null) messages.Show("Select a tower first");
        }

        // The selection changed: show the card for 'tower', or hide it.
        private void Show(Tower tower)
        {
            Watch(tower);
            if (card == null) return;
            card.gameObject.SetActive(tower != null);
            if (tower == null) return;
            nameLabel.text = tower.Data.displayName;
            if (fixedTag != null) fixedTag.SetActive(tower.IsFixed);
            Refresh(tower);
        }

        private void Watch(Tower tower)
        {
            if (shown != null) shown.StrategyChanged -= Refresh;
            shown = tower;
            if (shown != null) shown.StrategyChanged += Refresh;
        }

        private void Refresh(Tower tower)
        {
            int active = TargetingSettings.IndexOf(tower.Strategy);
            targetLabel.text = targetTexts[active];
            for (int i = 0; i < modeButtons.Length; i++)
            {
                bool on = i == active;
                modeButtons[i].targetGraphic.color = on ? activeButton : idleButton;
                TMP_Text label = modeButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.color = on ? activeText : idleText;
            }
        }

        private void Layout()
        {
            if (card == null || grid == null) return;
            bool isometric = grid.ProjectionType == GridProjectionType.Isometric;
            card.anchoredPosition = new Vector2(-(isometric ? IsoMargin : TopDownMargin), BottomY);
            card.localScale = Vector3.one * (isometric ? 1f : TopDownScale);
        }
    }
}
