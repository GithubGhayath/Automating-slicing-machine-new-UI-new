namespace DataAccess.Enums
{
    /// <summary>
    /// The physical unit an element's life is measured in. Bearings wear per
    /// revolution, V-belts wear per pass around the pulley loop, so the two can
    /// never be added together or compared directly.
    /// </summary>
    public enum enElementLifeUnit
    {
        Revolution = 1,
        Pass = 2
    }
}
