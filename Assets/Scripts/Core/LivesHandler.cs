using UnityEngine;

namespace TowerDefense
{
    // Connects enemies to the player's lives: every enemy that reaches the goal costs one life.
    // Kept separate so Enemy does not need to know about lives at all.
    public class LivesHandler : MonoBehaviour
    {
        [SerializeField] private PlayerResources resources;

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
            if (resources != null) resources.LoseLife(1);
        }
    }
}
