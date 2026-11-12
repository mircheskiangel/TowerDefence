using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerDefense
{
    // The audio settings: a speaker button (top right corner on the level select, under the targeting
    // selector in a level) opens a panel with Master, Music and Sfx sliders and a Mute toggle. The values
    // live in AudioManager (saved in PlayerPrefs); this class only shows and edits them. The M key, handled
    // by AudioManager, mutes everywhere; the toggle follows it. It sits on an always-active object.
    public class AudioSettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button openButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Toggle muteToggle;
        [SerializeField] private Vector2 menuButtonPosition = new Vector2(-20f, -20f);
        [SerializeField] private Vector2 gameButtonPosition = new Vector2(-20f, -84f);

        private AudioManager Audio => AudioManager.Instance;

        // Other input (the targeting arrow keys) is ignored while the panel is open.
        public bool IsOpen => panel != null && panel.activeSelf;

        private void Awake()
        {
            if (panel != null) panel.SetActive(false);
            // A short click when the sfx slider is released, as a preview of the new volume.
            EventTrigger trigger = sfxSlider.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            entry.callback.AddListener(_ => AudioManager.Play(SfxId.UiClick));
            trigger.triggers.Add(entry);
        }

        private void OnEnable()
        {
            openButton.onClick.AddListener(Toggle);
            closeButton.onClick.AddListener(Close);
            masterSlider.onValueChanged.AddListener(SetMaster);
            musicSlider.onValueChanged.AddListener(SetMusic);
            sfxSlider.onValueChanged.AddListener(SetSfx);
            muteToggle.onValueChanged.AddListener(SetMute);
        }

        private void OnDisable()
        {
            openButton.onClick.RemoveListener(Toggle);
            closeButton.onClick.RemoveListener(Close);
            masterSlider.onValueChanged.RemoveListener(SetMaster);
            musicSlider.onValueChanged.RemoveListener(SetMusic);
            sfxSlider.onValueChanged.RemoveListener(SetSfx);
            muteToggle.onValueChanged.RemoveListener(SetMute);
            if (Audio != null) Audio.SettingsChanged -= Refresh;
        }

        private void Start()
        {
            bool menu = LevelManager.Instance == null || LevelManager.Instance.InMenu;
            ((RectTransform)openButton.transform).anchoredPosition = menu ? menuButtonPosition : gameButtonPosition;
            if (Audio != null) Audio.SettingsChanged += Refresh;
            Refresh();
        }

        // Shows the saved values without raising the sliders' events.
        private void Refresh()
        {
            if (Audio == null) return;
            masterSlider.SetValueWithoutNotify(Audio.MasterVolume);
            musicSlider.SetValueWithoutNotify(Audio.MusicVolume);
            sfxSlider.SetValueWithoutNotify(Audio.SfxVolume);
            muteToggle.SetIsOnWithoutNotify(Audio.Muted);
        }

        public void Toggle()
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) Refresh();
        }

        public void Close()
        {
            panel.SetActive(false);
        }

        private void SetMaster(float value) { if (Audio != null) Audio.SetMasterVolume(value); }
        private void SetMusic(float value) { if (Audio != null) Audio.SetMusicVolume(value); }
        private void SetSfx(float value) { if (Audio != null) Audio.SetSfxVolume(value); }
        private void SetMute(bool value) { if (Audio != null) Audio.SetMuted(value); }
    }
}
