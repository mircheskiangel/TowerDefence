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

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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

        // Used in M5 when an enemy reaches the goal.
        public void LoseLife(int amount = 1)
        {
            if (amount <= 0 || Lives <= 0) return;
            Lives = Mathf.Max(0, Lives - amount);
            Changed?.Invoke();
        }
    }
}
