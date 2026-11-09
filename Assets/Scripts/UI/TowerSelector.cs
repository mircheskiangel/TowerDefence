using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerDefense
{
    // The tower selector, in both views: one card per tower the level allows (LevelData.allowedTowers),
    // chosen by clicking a card or with the keys 1 and 2. The selected card is highlighted; a card
    // is greyed out while the player cannot afford it or its limit is reached (it can still be
    // selected, and placing then gives the matching message). TowerPlacer holds the selection and
    // the rules; this class only shows them. It sits on an always-active object.
    public class TowerSelector : MonoBehaviour
    {
        // Isometric: the bottom left corner. Top-down: smaller, in the margin left of the board.
        private const float IsoX = 20f, TopDownX = 8f, BottomY = 16f, IsoStep = 136f, TopDownStep = 92f, TopDownScale = 0.66f;

        [SerializeField] private GridManager grid;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private TowerPlacer placer;
        [SerializeField] private SkinSet skin;
        [SerializeField] private TowerCardView[] cards = new TowerCardView[0];

        private IReadOnlyList<TowerSO> towers;

        private void OnEnable()
        {
            if (grid != null) grid.ProjectionChanged += Layout;
            if (resources != null) resources.Changed += Refresh;
            if (placer != null)
            {
                placer.SelectionChanged += Refresh;
                placer.TowersChanged += Refresh;
            }
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                cards[i].Button.onClick.AddListener(() => Select(index));
            }
        }

        private void OnDisable()
        {
            if (grid != null) grid.ProjectionChanged -= Layout;
            if (resources != null) resources.Changed -= Refresh;
            if (placer != null)
            {
                placer.SelectionChanged -= Refresh;
                placer.TowersChanged -= Refresh;
            }
            foreach (TowerCardView card in cards) card.Button.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            bool menu = LevelManager.Instance == null || LevelManager.Instance.InMenu;
            towers = menu ? null : LevelManager.Instance.AllowedTowers;
            int count = towers != null ? towers.Count : 0;
            for (int i = 0; i < cards.Length; i++)
            {
                if (i < count) cards[i].Bind(towers[i], skin != null ? skin.Get(towers[i].bodySlot) : null, count > 1);
                else cards[i].Hide();
            }
            Layout();
            Refresh();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame) Select(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) Select(1);
        }

        // Clicks and hotkeys end here. Public so the rules can be tested without real input.
        public void Select(int index)
        {
            if (towers != null && index >= 0 && index < towers.Count) placer.SelectTower(towers[index]);
        }

        // The first card on top, the last one in the corner (a single card stays where it always was).
        private void Layout()
        {
            if (towers == null) return;
            bool isometric = grid.ProjectionType == GridProjectionType.Isometric;
            for (int i = 0; i < towers.Count && i < cards.Length; i++)
            {
                cards[i].Rect.anchoredPosition = new Vector2(isometric ? IsoX : TopDownX,
                    BottomY + (towers.Count - 1 - i) * (isometric ? IsoStep : TopDownStep));
                cards[i].Rect.localScale = Vector3.one * (isometric ? 1f : TopDownScale);
            }
        }

        private void Refresh()
        {
            if (towers == null) return;
            for (int i = 0; i < towers.Count && i < cards.Length; i++)
            {
                TowerSO tower = towers[i];
                int limit = placer.LimitOf(tower);
                bool limited = placer.IsLimitReached(tower);
                string counter = limit <= 0 ? "" : limited ? $"Max {limit}" : $"{placer.CountLive(tower)}/{limit}";
                cards[i].Refresh(placer.SelectedTower == tower, limited || !resources.CanAfford(tower.cost), counter);
            }
        }
    }
}
