namespace MR200.UI.Simulation
{
    /// <summary>
    /// Simulates production output. The instantaneous rate (slices per minute)
    /// is derived from the constant feed rate and product length, fluctuates
    /// realistically, and the total accumulates continuously while running.
    /// </summary>
    public sealed class ProductionSimulation
    {
        private readonly SensorSignal _rate;

        public ProductionSimulation(double nominalRatePerMinute, int seed)
        {
            // Rate hovers around the nominal value with gentle fluctuation and rare dips.
            _rate = new SensorSignal(
                mean: nominalRatePerMinute,
                theta: 0.6,
                sigma: nominalRatePerMinute * 0.06,
                min: nominalRatePerMinute * 0.75,
                max: nominalRatePerMinute * 1.15,
                spikeChancePerSecond: 0.02,
                spikeMagnitude: nominalRatePerMinute * 0.1,
                seed: seed);
        }

        public double CurrentRate { get; private set; }   // slices / minute
        public double Total { get; private set; }          // accumulated slices
        public double ElapsedMinutes { get; private set; }
        public double AverageRate => ElapsedMinutes > 0 ? Total / ElapsedMinutes : 0;

        public void Tick(double dt)
        {
            CurrentRate = _rate.Tick(dt);
            double dtMinutes = dt / 60.0;
            Total += CurrentRate * dtMinutes;
            ElapsedMinutes += dtMinutes;
        }

        public void Reset()
        {
            _rate.Reset();
            CurrentRate = 0;
            Total = 0;
            ElapsedMinutes = 0;
        }
    }
}
