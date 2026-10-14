using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense
{
    // Strategy interface for target selection: given the enemies a tower can reach,
    // return the one it should attack (or null if the list is empty).
    // Each TargetingMode has its own implementation, so the tower never needs a switch.
    public interface ITargetSelector
    {
        Enemy SelectTarget(Vector2 towerPosition, IReadOnlyList<Enemy> inRange);
    }
}
