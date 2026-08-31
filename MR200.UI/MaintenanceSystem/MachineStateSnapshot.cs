namespace MR200.UI.MaintenanceSystem
{
    /// <summary>
    /// The machine's condition at the exact moment an element reached its rated life.
    ///
    /// Every value is captured from the calculations the application already performs -
    /// nothing here is recomputed independently.
    /// </summary>
    public sealed class MachineStateSnapshot
    {
        public string MachineStatus { get; init; } = "Stopped";
        public string ProductionState { get; init; } = "Cutting operation interrupted";

        /// <summary>Wall-clock time the machine has been running, as shown on the machine timer.</summary>
        public string MachineRuntime { get; init; } = "00:00:00";

        public double MachineRuntimeInHours { get; init; }

        /// <summary>Volume produced since machine start, m3. Same formula the End Process path uses.</summary>
        public double ProductionQuantityInCubicMeter { get; init; }

        /// <summary>Electricity figure since machine start, from the existing energy calculation.</summary>
        public double ConsumedElectricity { get; init; }

        /// <summary>Live cutting-shaft speed, from the application's RPM calculation.</summary>
        public double MachineSpeedInRPM { get; init; }

        public string WoodType { get; init; } = "N/A";
        public DateTime FailureDetectedAt { get; init; } = DateTime.Now;
    }
}
