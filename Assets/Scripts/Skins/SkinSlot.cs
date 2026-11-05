namespace TowerDefense
{
    // Visual only: the named sprite slots of a SkinSet. Slots marked "variants" hold several
    // sprites (e.g. grass tiles); the consumer picks one with a fixed index (e.g. per cell).
    public enum SkinSlot
    {
        None,
        GrassTile,      // variants, floor tile (pivot: center)
        PathTile,       // variants, floor tile on the enemy path (pivot: center)
        SpawnPortal,    // standing object (pivot: feet)
        GoalGate,       // standing object (pivot: feet)
        TowerBody,
        TowerBase,
        TowerShadow,
        Grunt,
        Tank,
        Tree,           // variants, decoration
        Rock,           // variants, decoration
        Bush,           // variants, decoration
        CliffLeft,
        CliffRight,
        Projectile,
        HudPanel,
        HudButton,
        Background,     // full-screen backdrop behind the island (isometric)
        GrassDetail,    // variants, tuft/flower overlay on a grass tile (pivot: center)
        GruntShadow,    // contact shadow under the Grunt ball (pivot: center = feet)
        TankShadow,     // contact shadow under the Tank ball
    }
}
