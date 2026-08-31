namespace DataAccess.Enums
{
    /// <summary>
    /// Predictive-maintenance health band derived from ConsumedLife / DefaultLife.
    /// </summary>
    public enum enElementHealthStatus
    {
        Normal = 1,
        Warning = 2,
        Critical = 3,
        Failed = 4
    }
}
