using UnityEngine;

namespace TowerDefense
{
    // Connects enemies to the player's lives: an enemy that reaches the goal costs one life, or all
    // of them if its data says so (a boss).
    // Kept separate so Enemy does not need to know about lives at all.
    public class LivesHandler : MonoBehaviour
    {
        [SerializeField] private PlayerResources resources;

        // Why the player lost at once; empty if lives simply ran out.
        public string InstantDefeatMessage { get; private set; } = "";

        private void OnEnable()
        {
            Enemy.OnReachedGoal += HandleEnemyReachedGoal;
        }

        private void OnDisable()
        {
            Enemy.OnReachedGoal -= HandleEnemyReachedGoal;
        }

        private void HandleEnemyReachedGoal(Enemy enemy)
        {
            if (resources == null) resources = PlayerResources.Instance;
            if (resources == null) return;
            if (enemy.Data.instantDefeatOnLeak)
            {
                InstantDefeatMessage = enemy.Data.defeatMessage; // Before the state change shows the panel.
                resources.LoseLife(resources.Lives);
            }
            else resources.LoseLife(1);
        }
    }
}
