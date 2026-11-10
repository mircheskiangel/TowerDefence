using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TowerDefense
{
    // The one sound player of the game. It is in the scene LevelBuilder builds; the first one survives
    // scene loads (DontDestroyOnLoad) and the copies in later loads destroy themselves.
    // It only listens to existing gameplay events, so gameplay never waits for or depends on audio:
    //   Tower.Fired, Projectile.HitEnemy, Enemy.OnDied, Enemy.OnReachedGoal, WaveManager.WaveStarted and
    //   EnemySpawned, GameManager.StateChanged, TowerPlacer.TowersChanged and PlacementRejected, and
    //   every UI Button's onClick.
    // Sound effects use a pool of AudioSources with a voice limit and a per-clip cooldown. Music uses two
    // sources for crossfades; jingles have their own source. Volumes are saved in PlayerPrefs.
    // Its own random generator is used for pitch and volume variation, so UnityEngine.Random (and with it
    // gameplay) is never touched. Nothing here allocates per frame.
    [DefaultExecutionOrder(-500)]
    public class AudioManager : MonoBehaviour
    {
        private const string MasterKey = "TowerDefense.Audio.Master";
        private const string MusicKey = "TowerDefense.Audio.Music";
        private const string SfxKey = "TowerDefense.Audio.Sfx";
        private const string MuteKey = "TowerDefense.Audio.Mute";
        public const float DefaultMaster = 0.8f, DefaultMusic = 0.5f, DefaultSfx = 0.8f;

        public static AudioManager Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        [SerializeField] private AudioLibrary library;
        [SerializeField] private int poolSize = 16;
        [Tooltip("At most this many sound effects play at the same time; more are dropped.")]
        [SerializeField] private int maxVoices = 12;
        [Tooltip("The same effect is not started again within this many seconds.")]
        [SerializeField] private float sameClipCooldown = 0.04f;
        [SerializeField] private float pitchVariation = 0.06f;
        [SerializeField] private float volumeVariation = 0.10f;
        [Tooltip("Stereo pan of tower and enemy sounds by their screen x position.")]
        [SerializeField] private float panRange = 0.4f;
        [SerializeField] private float crossfadeSeconds = 1f;
        [SerializeField] private float endFadeSeconds = 0.3f;

        // Settings (0..1) and mute.
        public float MasterVolume { get; private set; } = DefaultMaster;
        public float MusicVolume { get; private set; } = DefaultMusic;
        public float SfxVolume { get; private set; } = DefaultSfx;
        public bool Muted { get; private set; }
        public event Action SettingsChanged;

        // Music state, for the UI and tests.
        public MusicId CurrentMusic { get; private set; } = MusicId.None;
        public bool JinglePlaying => jingleSource != null && jingleSource.isPlaying;
        // Raised with a short description on every music change (crossfade, end fade, jingle).
        public event Action<string> MusicChanged;

        // Counters for tests: requests per effect, effects started, effects dropped by cooldown or voice limit.
        public readonly int[] Requested = new int[(int)SfxId.Count];
        public readonly int[] Played = new int[(int)SfxId.Count];
        public int DroppedByCooldown { get; private set; }
        public int DroppedByVoiceLimit { get; private set; }
        public int MaxVoicesUsed { get; private set; }
        // Test switch: while suspended nothing new is played (used to measure the cost of audio).
        public bool Suspended { get; set; }

        private AudioSource[] pool;
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly float[] musicLevel = new float[2];
        private readonly float[] musicTarget = new float[2];
        private readonly float[] musicFadeSeconds = new float[2];
        private AudioSource jingleSource;
        private int activeMusic;
        private readonly float[] lastPlayed = new float[(int)SfxId.Count];
        private uint rng = 0x9E3779B9u;
        private Camera cam;
        private Scene boundScene;

        // Cached handlers, so subscribing allocates nothing after the first time.
        private Action<Tower> onFired;
        private Action<Enemy> onHit, onDied, onReachedGoal, onEnemySpawned;
        private Action<int> onWaveStarted;
        private Action<GameState> onStateChanged;
        private Action onPlaced, onRejected;
        private UnityEngine.Events.UnityAction onClick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject); // The scene's copy after a reload: the first one keeps playing.
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            pool = new AudioSource[poolSize];
            for (int i = 0; i < poolSize; i++) pool[i] = NewSource(false);
            for (int i = 0; i < 2; i++) music[i] = NewSource(true);
            jingleSource = NewSource(false);
            for (int i = 0; i < lastPlayed.Length; i++) lastPlayed[i] = -10f;

            onFired = HandleFired;
            onHit = HandleHit;
            onDied = HandleDied;
            onReachedGoal = HandleReachedGoal;
            onEnemySpawned = HandleEnemySpawned;
            onWaveStarted = HandleWaveStarted;
            onStateChanged = HandleStateChanged;
            onPlaced = HandlePlaced;
            onRejected = HandleRejected;
            onClick = HandleClick;

            LoadSettings();
            ApplyVolumes();
            Tower.Fired += onFired;
            Projectile.HitEnemy += onHit;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            Bind(SceneManager.GetActiveScene()); // In case sceneLoaded came before this object existed.
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Tower.Fired -= onFired;
            Projectile.HitEnemy -= onHit;
            Enemy.OnDied -= onDied;
            Enemy.OnReachedGoal -= onReachedGoal;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }

        private AudioSource NewSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f; // 2D sound.
            source.priority = loop ? 0 : 128;
            return source;
        }

        // ------------------------------------------------------------------ Scene binding

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Bind(scene);
        }

        // Every level load is a new scene: the new scene objects are found and their events subscribed.
        // GameManager clears Enemy's static events when a level starts, so they are subscribed again here.
        private void Bind(Scene scene)
        {
            if (boundScene.IsValid() && scene == boundScene) return;
            boundScene = scene;
            cam = Camera.main;

            Enemy.OnDied -= onDied;
            Enemy.OnDied += onDied;
            Enemy.OnReachedGoal -= onReachedGoal;
            Enemy.OnReachedGoal += onReachedGoal;

            WaveManager waves = FindAnyObjectByType<WaveManager>();
            if (waves != null)
            {
                waves.WaveStarted += onWaveStarted;
                waves.EnemySpawned += onEnemySpawned;
            }
            GameManager game = FindAnyObjectByType<GameManager>();
            if (game != null) game.StateChanged += onStateChanged;
            TowerPlacer placer = FindAnyObjectByType<TowerPlacer>();
            if (placer != null)
            {
                placer.TowersChanged += onPlaced;
                placer.PlacementRejected += onRejected;
            }
            foreach (Button button in FindObjectsByType<Button>(FindObjectsInactive.Include))
            {
                button.onClick.RemoveListener(onClick);
                button.onClick.AddListener(onClick);
            }

            // Returning to the menu or (re)starting a level ends any jingle and picks the music.
            if (jingleSource.isPlaying)
            {
                jingleSource.Stop();
                Log("jingle stopped");
            }
            bool menu = LevelManager.Instance != null && LevelManager.Instance.InMenu;
            PlayMusic(menu ? MusicId.Menu : MusicId.Game);
        }

        // ------------------------------------------------------------------ Gameplay events

        private void HandleFired(Tower tower)
        {
            if (tower != null && tower.Data != null) PlaySfx(tower.Data.fireSound, tower.transform.position);
        }

        private void HandleHit(Enemy enemy)
        {
            if (enemy != null && enemy.Data != null) PlaySfx(enemy.Data.hitSound, enemy.transform.position);
        }

        private void HandleDied(Enemy enemy)
        {
            if (enemy != null && enemy.Data != null) PlaySfx(enemy.Data.deathSound, enemy.transform.position);
        }

        private void HandleReachedGoal(Enemy enemy)
        {
            PlaySfx(SfxId.LifeLost);
        }

        private void HandleWaveStarted(int wave)
        {
            PlaySfx(SfxId.WaveStart);
        }

        private void HandleEnemySpawned(Enemy enemy)
        {
            if (enemy == null || enemy.Data == null || !enemy.Data.isBoss) return;
            PlaySfx(SfxId.BossApproach);
            PlayMusic(MusicId.Boss);
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Won) PlayJingle(JingleId.Win);
            else if (state == GameState.Lost) PlayJingle(JingleId.Lose);
        }

        private void HandlePlaced() { PlaySfx(SfxId.PlaceTower); }
        private void HandleRejected() { PlaySfx(SfxId.Blocked); }
        private void HandleClick() { PlaySfx(SfxId.UiClick); }

        // ------------------------------------------------------------------ Sound effects

        public static void Play(SfxId id)
        {
            if (Instance != null) Instance.PlaySfx(id);
        }

        public void PlaySfx(SfxId id)
        {
            PlaySfx(id, 0f);
        }

        // A world position pans the sound by where it is on screen.
        public void PlaySfx(SfxId id, Vector3 worldPosition)
        {
            float pan = 0f;
            if (cam != null)
            {
                float x = cam.WorldToViewportPoint(worldPosition).x;
                pan = Mathf.Clamp((x - 0.5f) * 2f, -1f, 1f) * panRange;
            }
            PlaySfx(id, pan);
        }

        private void PlaySfx(SfxId id, float pan)
        {
            int i = (int)id;
            if (i < 0 || i >= (int)SfxId.Count) return;
            Requested[i]++;
            if (Suspended || library == null) return;
            AudioClip clip = library.Sfx(id);
            if (clip == null) return;

            float now = Time.unscaledTime;
            if (now - lastPlayed[i] < sameClipCooldown)
            {
                DroppedByCooldown++;
                return;
            }
            int busy = 0;
            AudioSource free = null;
            for (int k = 0; k < pool.Length; k++)
            {
                if (pool[k].isPlaying) busy++;
                else if (free == null) free = pool[k];
            }
            if (busy >= maxVoices || free == null)
            {
                DroppedByVoiceLimit++;
                return;
            }

            lastPlayed[i] = now;
            free.clip = clip;
            free.pitch = 1f + pitchVariation * NextSigned();
            free.volume = Mathf.Clamp01(library.SfxVolume(id) * (1f + volumeVariation * NextSigned()) * SfxVolume);
            free.panStereo = pan;
            free.Play();
            Played[i]++;
            if (busy + 1 > MaxVoicesUsed) MaxVoicesUsed = busy + 1;
        }

        public int VoicesPlaying()
        {
            int busy = 0;
            for (int k = 0; k < pool.Length; k++) if (pool[k].isPlaying) busy++;
            return busy;
        }

        public void ResetCounters()
        {
            Array.Clear(Requested, 0, Requested.Length);
            Array.Clear(Played, 0, Played.Length);
            DroppedByCooldown = DroppedByVoiceLimit = MaxVoicesUsed = 0;
        }

        // xorshift32: -1..1, independent of UnityEngine.Random.
        private float NextSigned()
        {
            rng ^= rng << 13;
            rng ^= rng >> 17;
            rng ^= rng << 5;
            return (rng / 4294967296f) * 2f - 1f;
        }

        // ------------------------------------------------------------------ Music and jingles

        // Crossfades to a track (nothing happens if it is already playing).
        public void PlayMusic(MusicId id)
        {
            if (id == CurrentMusic && (id == MusicId.None || music[activeMusic].isPlaying)) return;
            AudioClip clip = library != null ? library.Music(id) : null;
            CurrentMusic = id;
            int previous = activeMusic;
            musicTarget[previous] = 0f;
            musicFadeSeconds[previous] = crossfadeSeconds;
            if (clip == null)
            {
                Log("music -> none");
                return;
            }
            activeMusic = 1 - previous;
            AudioSource source = music[activeMusic];
            source.clip = clip;
            source.time = 0f;
            musicLevel[activeMusic] = 0f;
            musicTarget[activeMusic] = 1f;
            musicFadeSeconds[activeMusic] = crossfadeSeconds;
            source.volume = 0f;
            if (!Suspended) source.Play();
            Log("music -> " + id);
        }

        // A win or a loss: the level music fades out fast and the jingle plays once, uninterrupted.
        public void PlayJingle(JingleId id)
        {
            for (int i = 0; i < 2; i++)
            {
                musicTarget[i] = 0f;
                musicFadeSeconds[i] = endFadeSeconds;
            }
            CurrentMusic = MusicId.None;
            AudioClip clip = library != null ? library.Jingle(id) : null;
            if (clip == null || Suspended) return;
            jingleSource.clip = clip;
            jingleSource.volume = MusicVolume;
            jingleSource.Play();
            Log("music fade out, jingle " + id);
        }

        // How many music sources are audible now (2 only during a crossfade); the jingle counts too.
        public int AudibleMusicSources()
        {
            int n = 0;
            for (int i = 0; i < 2; i++) if (music[i].isPlaying && music[i].volume > 0.001f) n++;
            if (jingleSource.isPlaying) n++;
            return n;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime; // Fades follow real time, not the game speed.
            for (int i = 0; i < 2; i++)
            {
                if (!music[i].isPlaying) continue;
                float step = dt / Mathf.Max(0.01f, musicFadeSeconds[i]);
                musicLevel[i] = Mathf.MoveTowards(musicLevel[i], musicTarget[i], step);
                music[i].volume = musicLevel[i] * MusicVolume;
                if (musicLevel[i] <= 0f && musicTarget[i] <= 0f) music[i].Stop();
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.mKey.wasPressedThisFrame) SetMuted(!Muted);
        }

        private void Log(string text)
        {
            MusicChanged?.Invoke(text);
        }

        // ------------------------------------------------------------------ Settings

        public void SetMasterVolume(float value) { MasterVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetMusicVolume(float value) { MusicVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetSfxVolume(float value) { SfxVolume = Mathf.Clamp01(value); ApplyAndSave(); }
        public void SetMuted(bool value) { Muted = value; ApplyAndSave(); }

        private void ApplyAndSave()
        {
            ApplyVolumes();
            SaveSettings();
            SettingsChanged?.Invoke();
        }

        private void ApplyVolumes()
        {
            AudioListener.volume = Muted ? 0f : MasterVolume;
            for (int i = 0; i < 2; i++) if (music[i] != null) music[i].volume = musicLevel[i] * MusicVolume;
            if (jingleSource != null) jingleSource.volume = MusicVolume;
        }

        private void LoadSettings()
        {
            try
            {
                MasterVolume = PlayerPrefs.GetFloat(MasterKey, DefaultMaster);
                MusicVolume = PlayerPrefs.GetFloat(MusicKey, DefaultMusic);
                SfxVolume = PlayerPrefs.GetFloat(SfxKey, DefaultSfx);
                Muted = PlayerPrefs.GetInt(MuteKey, 0) == 1;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AudioManager] Could not read the audio settings: " + e.Message);
            }
        }

        private void SaveSettings()
        {
            try
            {
                PlayerPrefs.SetFloat(MasterKey, MasterVolume);
                PlayerPrefs.SetFloat(MusicKey, MusicVolume);
                PlayerPrefs.SetFloat(SfxKey, SfxVolume);
                PlayerPrefs.SetInt(MuteKey, Muted ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AudioManager] Could not save the audio settings: " + e.Message);
            }
        }

        // Back to the defaults (Tower Defense > Reset Audio Settings, and tests).
        public static void ResetSavedSettings()
        {
            try
            {
                PlayerPrefs.DeleteKey(MasterKey);
                PlayerPrefs.DeleteKey(MusicKey);
                PlayerPrefs.DeleteKey(SfxKey);
                PlayerPrefs.DeleteKey(MuteKey);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AudioManager] Could not reset the audio settings: " + e.Message);
            }
        }
    }
}
