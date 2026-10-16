using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerDefense
{
    public enum GameState { Playing, Won, Lost }

    // The game's state machine: Playing -> Won or Playing -> Lost, exactly once.
    // Lost comes from PlayerResources.LivesDepleted, Won from WaveManager.AllWavesCleared.
    // Leaving Playing freezes gameplay (Time.timeScale = 0); Restart() reloads the scene.
    // Runs before every other script so it can reset static state when the scene starts.
    [DefaultExecutionOrder(-1000)]
    public class GameManager : MonoBehaviour
    {
        [SerializeField] private PlayerResources resources;
        [SerializeField] private WaveManager waves;

        private bool winPending;

        public GameState State { get; private set; } = GameState.Playing;
        public bool IsPlaying => State == GameState.Playing;

        public event Action<GameState> StateChanged;

        private void Awake()
        {
            // A play session or a restarted level must never start frozen or at a leftover speed.
            Time.timeScale = 1f;
            ResetStaticState();
        }

        // With Domain Reload off, static fields keep their values between plays and across
        // scene reloads. This runs first in the new scene (execution order -1000), before any
        // other object's Awake/OnEnable subscribes or registers, so nothing old survives.
        private static void ResetStaticState()
        {
            EnemyRegistry.Clear();
            Enemy.ClearStaticEvents();
            TargetingSettings.ResetToDefault();
        }

        private void OnEnable()
        {
            if (resources != null) resources.LivesDepleted += HandleLivesDepleted;
            if (waves != null) waves.AllWavesCleared += HandleAllWavesCleared;
        }

        private void OnDisable()
        {
            if (resources != null) resources.LivesDepleted -= HandleLivesDepleted;
            if (waves != null) waves.AllWavesCleared -= HandleAllWavesCleared;
        }

        // Losing is applied immediately.
        private void HandleLivesDepleted()
        {
            SetState(GameState.Lost);
        }

        // Winning is only applied at the end of the frame (LateUpdate). If the player also
        // runs out of lives in that same frame, Lost has already been set and wins the tie.
        private void HandleAllWavesCleared()
        {
            winPending = true;
        }

        private void LateUpdate()
        {
            if (!winPending) return;
            winPending = false;
            SetState(GameState.Won);
        }

        // The only place the state changes. It only ever leaves Playing, and only once,
        // so a finished game can never flip from Won to Lost (or back).
        private void SetState(GameState newState)
        {
            if (State != GameState.Playing || newState == GameState.Playing) return;

            State = newState;
            winPending = false;
            Time.timeScale = 0f; // Freeze enemies, towers, projectiles and countdowns.
            Debug.Log($"[GameManager] State -> {newState}");
            StateChanged?.Invoke(newState);
        }

        // Time.timeScale is global and is NOT reset by loading a scene, so it must be set back
        // to 1 before reloading; otherwise the new level would start frozen.
        public void Restart()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }
    }
}
