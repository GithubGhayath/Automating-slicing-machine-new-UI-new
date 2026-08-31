using DataAccess.Entities;

namespace MR200.UI.MaintenanceSystem
{
    /// <summary>
    /// Everything the maintenance alert needs, gathered once after a failure so the
    /// e-mail builder never touches the database or the machine itself.
    /// </summary>
    public sealed class MaintenanceAlertContext
    {
        public required MonitoredElement FailedElement { get; init; }

        /// <summary>Consumed life at the instant the element reached its rated life.</summary>
        public double ConsumedLifeAtFailure { get; init; }

        public required MachineStateSnapshot MachineState { get; init; }

        /// <summary>Elements at or past the warning threshold, worst first.</summary>
        public IReadOnlyList<MonitoredElement> ElementsApproachingFailure { get; init; }
            = Array.Empty<MonitoredElement>();

        /// <summary>Maintenance carried out on the failed element inside the history window.</summary>
        public IReadOnlyList<Maintenance> FailedElementRecentMaintenance { get; init; }
            = Array.Empty<Maintenance>();

        /// <summary>Most recent maintenance anywhere on the machine, or null when there is none.</summary>
        public Maintenance? LastMachineMaintenance { get; init; }

        public int MaintenanceHistoryWindowInDays { get; init; } = 30;
        public double WarningThresholdPercent { get; init; } = 80;
    }
}
