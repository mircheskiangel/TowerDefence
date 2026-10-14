namespace TowerDefense
{
    // How a tower chooses which enemy in range to attack.
    public enum TargetingMode
    {
        First,      // Furthest along the path (closest to the goal).
        Closest,    // Nearest to the tower.
        Strongest,  // Highest current health.
        Weakest     // Lowest current health.
    }
}
