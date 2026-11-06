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
        HealthBarFrame, // 9-sliced dark border + dark-red track of the enemy health bar
        PortalGlow,     // pulsing halo laid over the spawn portal
        Puff,           // dust particle of the tower placement puff
        IconCoin,       // HUD icons
        IconHeart,
        IconArrow,      // left arrow; the right one is mirrored
        GruntWalk,      // variants = walk frames of the goblin (pivot: feet, facing right)
        TankWalk,       // variants = walk frames of the ogre (pivot: feet, facing right)
        GruntCharacterShadow, // ground shadow under the goblin
        TankCharacterShadow,  // ground shadow under the ogre
        Wall,           // variants, low boulder on a permanent wall cell (pivot: feet)
        WallShadow,     // soft ground shadow under a boulder (pivot: center = feet)
        TankEliteWalk,  // variants = walk frames of the elite ogre (pivot: feet, facing right)
        TankEliteCharacterShadow, // ground shadow under the elite ogre
        TankElite,      // elite Tank ball with a golden rim (Balls style)
        TankEliteShadow,// contact shadow under the elite ball
        IconLock,       // lock of a locked level on the level select screen
        SiegeBody,      // the Siege Cannon (pivot: feet)
        SiegeShadow,    // ground shadow under it
        SiegeProjectile,// its heavy shot (pivot: center)
    }
}
