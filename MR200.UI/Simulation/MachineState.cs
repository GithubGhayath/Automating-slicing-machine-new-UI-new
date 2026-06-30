namespace MR200.UI.Simulation
{
    public enum MachineState
    {
        Standby,   // never started, or fully reset (End Process)
        Running,   // Start pressed – everything advancing
        Paused     // Stop pressed – frozen, no values reset
    }
}
