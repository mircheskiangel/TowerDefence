using System;
using UnityEngine;

namespace TowerDefense
{
    // The player's economy and health: gold and lives.
    // Everything that changes them goes through these methods, and the UI listens to Changed.
    public class PlayerResources : MonoBehaviour
    {
        public static PlayerResources Instance { get; private set; }

        [SerializeField] private int startGold = 100;
        [SerializeField] private int startLives = 10;

        public int Gold { get; private set; }
        public int Lives { get; private set; }

        // Raised whenever gold or lives change.
        public event Action Changed;

        // Raised exactly once, when lives drop to 0.
        public event Action LivesDepleted;

        private void Awake()
        {
            // Always take over: after a scene reload the old instance may not be destroyed yet.
            Instance = this;
            Gold = startGold;
            Lives = startLives;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public bool CanAfford(int amount)
        {
            return Gold >= amount;
        }

        // Spends the gold if the player has enough; returns false (and spends nothing) otherwise.
        public bool TrySpend(int amount)
        {
            if (amount < 0 || Gold < amount) return false;
            Gold -= amount;
            Changed?.Invoke();
            return true;
        }

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
        }

        // Called when an enemy reaches the goal. Lives never go below 0, and because nothing
        // happens once Lives is 0, LivesDepleted can only be raised one time.
        public void LoseLife(int amount = 1)
        {
            if (amount <= 0 || Lives <= 0) return;
            Lives = Mathf.Max(0, Lives - amount);
            Changed?.Invoke();
            if (Lives == 0) LivesDepleted?.Invoke();
        }
    }
}
