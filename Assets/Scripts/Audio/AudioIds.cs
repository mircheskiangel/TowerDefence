namespace TowerDefense
{
    // Every sound effect. The values index AudioLibrary's arrays, so new ones go before Count.
    public enum SfxId
    {
        ShootBasic,
        ShootSiege,
        HitGrunt,
        HitTank,
        HitBoss,
        Death,
        DeathBoss,
        UiClick,
        PlaceTower,
        Blocked,
        LifeLost,
        WaveStart,
        BossApproach,
        Count
    }

    public enum MusicId
    {
        None,
        Menu,
        Game,
        Boss
    }

    public enum JingleId
    {
        Win,
        Lose
    }
}
