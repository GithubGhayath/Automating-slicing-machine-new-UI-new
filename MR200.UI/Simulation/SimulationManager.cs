using MR200.UI.Helpers;

namespace MR200.UI.Simulation
{
    /// <summary>
    /// Central simulation engine for the machine-monitoring dashboard.
    ///
    /// Owns every simulated sensor (cutting shafts, feed shafts, conveyors) and
    /// the production model, advances them all on a single <see cref="Tick"/>,
    /// and exposes the machine running state. The ViewModel only reads values
    /// from here – no simulation logic lives in the UI layer.
    ///
    /// Replacing the simulation with real PLC/sensor data later means feeding
    /// live readings into the same MonitoredSignal objects; the dashboard,
    /// statistics and charts stay untouched.
    /// </summary>
    public sealed class SimulationManager
    {
        public SimulationManager()
        {
            // Constant feed rate comes from the existing project configuration
            // (App.config "FeedVelocity", m/min) rather than an invented value.
            FeedRateMetersPerMinute = ReadDouble("FeedVelocity", 11.0);

            // Product length drives how many slices are produced per minute.
            double productLengthMeters = 0.2; // 200 mm product height (see seed data)
            double nominalSlicesPerMinute = FeedRateMetersPerMinute / productLengthMeters;

            // --- Cutting shafts: heavy primary torque, ~55 N·m with load spikes ---
            CuttingShaftA = new MonitoredSignal("Cutting Shaft A",
                new SensorSignal(mean: 55, theta: 0.9, sigma: 6.5, min: 30, max: 90,
                                 spikeChancePerSecond: 0.07, spikeMagnitude: 45, seed: 101));
            CuttingShaftB = new MonitoredSignal("Cutting Shaft B",
                new SensorSignal(mean: 58, theta: 0.8, sigma: 7.0, min: 32, max: 95,
                                 spikeChancePerSecond: 0.06, spikeMagnitude: 50, seed: 202));

            // --- Four feed shafts: smaller motors, slightly different behaviour each ---
            FeedShafts = new[]
            {
                new MonitoredSignal("Feed Shaft 1", new SensorSignal(22, 1.0, 3.0, 12, 40, 0.05, 18, 311)),
                new MonitoredSignal("Feed Shaft 2", new SensorSignal(25, 0.9, 3.4, 14, 44, 0.05, 20, 322)),
                new MonitoredSignal("Feed Shaft 3", new SensorSignal(20, 1.1, 2.8, 11, 38, 0.04, 16, 333)),
                new MonitoredSignal("Feed Shaft 4", new SensorSignal(24, 0.95, 3.2, 13, 42, 0.05, 19, 344)),
            };

            // --- Conveyors: belt speed centred on the constant feed rate ---
            InputConveyor = new MonitoredSignal("Input Conveyor",
                new SensorSignal(FeedRateMetersPerMinute, 1.4, 0.25, FeedRateMetersPerMinute - 1.2, FeedRateMetersPerMinute + 1.2, 0.015, 0.8, 411));
            OutputConveyor = new MonitoredSignal("Output Conveyor",
                new SensorSignal(FeedRateMetersPerMinute * 1.02, 1.3, 0.28, FeedRateMetersPerMinute - 1.0, FeedRateMetersPerMinute + 1.4, 0.015, 0.9, 422));

            Production = new ProductionSimulation(nominalSlicesPerMinute, seed: 511);
        }

        public MachineState State { get; private set; } = MachineState.Standby;
        public double ElapsedSeconds { get; private set; }
        public double FeedRateMetersPerMinute { get; }

        public MonitoredSignal CuttingShaftA { get; }
        public MonitoredSignal CuttingShaftB { get; }
        public MonitoredSignal[] FeedShafts { get; }
        public MonitoredSignal InputConveyor { get; }
        public MonitoredSignal OutputConveyor { get; }
        public ProductionSimulation Production { get; }

        public void Start() => State = MachineState.Running;
        public void Pause() => State = MachineState.Paused;

        public void Reset()
        {
            State = MachineState.Standby;
            ElapsedSeconds = 0;
            CuttingShaftA.Reset();
            CuttingShaftB.Reset();
            foreach (var s in FeedShafts) s.Reset();
            InputConveyor.Reset();
            OutputConveyor.Reset();
            Production.Reset();
        }

        /// <summary>Advances every sensor when running. Returns true if anything moved.</summary>
        public bool Tick(double dt)
        {
            if (State != MachineState.Running) return false;

            ElapsedSeconds += dt;
            CuttingShaftA.Tick(dt);
            CuttingShaftB.Tick(dt);
            foreach (var s in FeedShafts) s.Tick(dt);
            InputConveyor.Tick(dt);
            OutputConveyor.Tick(dt);
            Production.Tick(dt);
            return true;
        }

        private static double ReadDouble(string key, double fallback)
        {
            try { return Convert.ToDouble(clsHelper.ReadFromConfiguration(key)); }
            catch { return fallback; }
        }
    }
}
