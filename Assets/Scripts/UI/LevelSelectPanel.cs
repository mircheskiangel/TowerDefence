using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerDefense
{
    // The level select screen, shown at the start (LevelManager.InMenu). One button per level;
    // a locked level shows a lock icon and a hint, and its button cannot be clicked.
    // This script sits on an always-active object; only the 'panel' child is shown or hidden.
    public class LevelSelectPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button[] buttons = new Button[0];
        [SerializeField] private TMP_Text[] labels = new TMP_Text[0];
        [Tooltip("Shown while the level with the same index is locked (may be empty for a level).")]
        [SerializeField] private GameObject[] lockIcons = new GameObject[0];
        [SerializeField] private GameObject[] lockHints = new GameObject[0];

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
        }

        private void OnEnable()
        {
            LevelProgress.Changed += Refresh;
            for (int i = 0; i < buttons.Length; i++)
            {
                int index = i;
                buttons[i].onClick.AddListener(() => Choose(index));
            }
        }

        private void OnDisable()
        {
            LevelProgress.Changed -= Refresh;
            foreach (Button button in buttons) button.onClick.RemoveAllListeners();
        }

        private void Start()
        {
            bool show = LevelManager.Instance != null && LevelManager.Instance.InMenu;
            if (panel != null) panel.SetActive(show);
            if (show) Refresh();
        }

        // Clicks end here. A locked level is rejected even if the button were reached somehow.
        public void Choose(int index)
        {
            if (LevelManager.Instance != null && LevelProgress.IsUnlocked(index))
                LevelManager.Instance.LoadLevel(index);
        }

        public void Refresh()
        {
            if (LevelManager.Instance == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                bool unlocked = LevelProgress.IsUnlocked(i);
                buttons[i].interactable = unlocked;
                labels[i].text = $"{i + 1}.  {LevelManager.Instance.LevelName(i)}";
                labels[i].alpha = unlocked ? 1f : 0.55f;
                if (lockIcons[i] != null) lockIcons[i].SetActive(!unlocked);
                if (lockHints[i] != null) lockHints[i].SetActive(!unlocked);
            }
        }
    }
}
