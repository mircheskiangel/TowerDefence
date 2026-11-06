using UnityEngine;

namespace TowerDefense
{
    // Applies the selected level when the scene starts: grid, resources, waves and board. It runs
    // before every other script (even GameManager), so they all see the level from their own
    // Awake on. With no level selected it shows the first level's board behind the level select
    // screen, with waves and tower placement switched off.
    // Changing level is a scene reload through GameManager.Restart, the same path as Restart, so
    // the timeScale reset and the static state reset apply to every level change.
    [DefaultExecutionOrder(-2000)]
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [SerializeField] private LevelData[] levels = new LevelData[0];
        [SerializeField] private GridManager grid;
        [SerializeField] private PlayerResources resources;
        [SerializeField] private WaveManager waves;
        [SerializeField] private TowerPlacer placer;
        [SerializeField] private BoardView board;
        [SerializeField] private GameManager gameManager;

        public LevelData Current { get; private set; }
        public int CurrentIndex { get; private set; }
        public bool InMenu { get; private set; }
        public int LevelCount => levels.Length;
        public bool HasNextLevel => CurrentIndex + 1 < levels.Length;

        private void Awake()
        {
            Instance = this; // Always take over: the old scene's instance may not be destroyed yet.
            int selected = LevelSession.SelectedIndex;
            InMenu = selected < 0 || selected >= levels.Length;
            CurrentIndex = InMenu ? 0 : selected;
            Current = levels[CurrentIndex];

            grid.ApplyLevel(Current);
            resources.Configure(Current.startingGold, Current.startingLives);
            waves.SetWaves(Current.waves);
            board.ApplyLevel(Current);

            if (InMenu)
            {
                waves.enabled = false;
                placer.enabled = false;
            }
        }

        private void OnEnable()
        {
            if (gameManager != null) gameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (gameManager != null) gameManager.StateChanged -= HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Won) LevelProgress.MarkWon(CurrentIndex);
        }

        public string LevelName(int index)
        {
            return index >= 0 && index < levels.Length ? levels[index].levelName : "";
        }

        public void LoadLevel(int index)
        {
            LevelSession.Select(index);
            gameManager.Restart();
        }

        public void LoadMenu()
        {
            LoadLevel(LevelSession.None);
        }

        // The win panel's second button: the next level, or the menu after the last one.
        public void LoadNextOrMenu()
        {
            if (HasNextLevel) LoadLevel(CurrentIndex + 1);
            else LoadMenu();
        }
    }
}
