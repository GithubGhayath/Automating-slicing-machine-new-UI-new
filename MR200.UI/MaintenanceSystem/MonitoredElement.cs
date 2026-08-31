using DataAccess.Entities;
using DataAccess.Enums;
using MR200.UI.Helpers;

namespace MR200.UI.MaintenanceSystem
{
    /// <summary>
    /// In-memory monitoring state for one physical element while the machine runs.
    ///
    /// <see cref="PersistedConsumedLife"/> is the cumulative value read from the
    /// database at RUN. <see cref="RuntimeAccumulatedLife"/> is what the current
    /// production operation has added since then and is NOT written to the database
    /// until the operation actually completes.
    /// </summary>
    public sealed class MonitoredElement
    {
        public MonitoredElement(Element element)
        {
            ElementId = element.Id;
            OrderOfElementAtMachine = element.OrderOfElementAtMachine;
            Description = element.Description;
            ImageOfElementAtMachine = element.ImageOfElementAtMachine;
            PersistedConsumedLife = element.ConsumedLife;
            FailureDetected = element.FailureDetected;

            var info = element.ElementInformation;
            ElementInformationId = info.Id;
            TypeName = info.Name;
            DefaultLife = info.DefaultLife;
            LifeUnit = info.LifeUnit;
            Price = info.Price;
            ImageUrl = info.Image;
            CatalogFileName = info.Catalog;

            DriveGroup = clsElementLifeEquations.DriveGroupOf(OrderOfElementAtMachine);
        }

        public int ElementId { get; }
        public int ElementInformationId { get; }
        public int OrderOfElementAtMachine { get; }
        public string Description { get; }
        public string TypeName { get; }
        public string? ImageOfElementAtMachine { get; }
        public string? ImageUrl { get; }
        public string? CatalogFileName { get; }
        public double Price { get; }
        public double DefaultLife { get; }
        public enElementLifeUnit LifeUnit { get; }
        public enElementDriveGroup DriveGroup { get; }

        /// <summary>Cumulative life read from the database when RUN was pressed.</summary>
        public double PersistedConsumedLife { get; private set; }

        /// <summary>Life added by the production operation currently in progress.</summary>
        public double RuntimeAccumulatedLife { get; private set; }

        /// <summary>Life consumed per second of running, in this element's own unit.</summary>
        public double LifeConsumedPerSecond { get; private set; }

        /// <summary>Latched once this element has reached its rated life.</summary>
        public bool FailureDetected { get; private set; }

        /// <summary>Persistent value plus whatever the current operation has added.</summary>
        public double TotalConsumedLife => PersistedConsumedLife + RuntimeAccumulatedLife;

        public double RemainingLife => clsElementLifeEquations.RemainingLife(TotalConsumedLife, DefaultLife);

        public double LifeUsedPercentage => clsElementLifeEquations.LifeUsedPercentage(TotalConsumedLife, DefaultLife);

        public enElementHealthStatus HealthStatus => clsElementLifeEquations.HealthStatus(
            LifeUsedPercentage, FailureDetected,
            MaintenanceSettings.WarningThresholdPercent,
            MaintenanceSettings.CriticalThresholdPercent);

        public string UnitLabel => clsElementLifeEquations.UnitLabel(LifeUnit);

        /// <summary>Recomputes the per-second rate for the machine's current speed.</summary>
        public void RefreshRate(double cuttingShaftSpeedInRPM)
        {
            LifeConsumedPerSecond = clsElementLifeEquations.LifeConsumedPerSecond(
                DriveGroup,
                cuttingShaftSpeedInRPM,
                MaintenanceSettings.FeedingShaftSpeedInRPM,
                MaintenanceSettings.BeltLinearSpeedInMeterPerSecond,
                MaintenanceSettings.BeltPitchLengthInMeter);
        }

        /// <summary>Adds the life earned over a real elapsed interval.</summary>
        public void Accumulate(double elapsedSeconds)
        {
            if (FailureDetected) return;
            RuntimeAccumulatedLife += clsElementLifeEquations.LifeAdded(LifeConsumedPerSecond, elapsedSeconds);
        }

        public bool HasReachedEndOfLife => !FailureDetected && DefaultLife > 0 && TotalConsumedLife >= DefaultLife;

        public void MarkFailed() => FailureDetected = true;

        /// <summary>
        /// Called after the runtime accumulation has been written to the database:
        /// the runtime total folds into the persisted total and resets to zero.
        /// </summary>
        public void OnAccumulationPersisted()
        {
            PersistedConsumedLife += RuntimeAccumulatedLife;
            RuntimeAccumulatedLife = 0;
        }
    }
}
