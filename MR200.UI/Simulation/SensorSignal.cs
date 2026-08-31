namespace MR200.UI.Simulation
{
    /// <summary>
    /// Generates a realistic industrial-sensor value over time.
    ///
    /// It is NOT a per-tick random number. The value follows a mean-reverting
    /// (Ornstein–Uhlenbeck) process which gives:
    ///   • smooth transitions and inertia (it drifts back toward an operating mean)
    ///   • natural fluctuation (Gaussian volatility)
    ///   • occasional load spikes that decay away
    ///   • a bounded operating range
    ///
    /// Every sensor in the machine is one of these. To swap in real PLC/sensor
    /// data later, replace the call to <see cref="Tick"/> with the live reading;
    /// nothing else in the dashboard needs to change.
    /// </summary>
    public sealed class SensorSignal
    {
        private readonly Random _rng;
        private readonly double _mean;        // operating set-point
        private readonly double _theta;       // reversion speed (inertia)
        private readonly double _sigma;       // volatility
        private readonly double _min;
        private readonly double _max;
        private readonly double _spikeChancePerSecond;
        private readonly double _spikeMagnitude;

        private double _value;
        private double _spike;                // transient load spike, decays over time
        private double _spare;                // cached Box–Muller sample
        private bool _hasSpare;

        public SensorSignal(double mean, double theta, double sigma, double min, double max,
                            double spikeChancePerSecond, double spikeMagnitude, int seed)
        {
            _mean = mean;
            _theta = theta;
            _sigma = sigma;
            _min = min;
            _max = max;
            _spikeChancePerSecond = spikeChancePerSecond;
            _spikeMagnitude = spikeMagnitude;
            _rng = new Random(seed);
            _value = mean;
        }

        /// <summary>When false the sensor reads a flat zero (simulates a stopped/failed shaft).</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Advances the signal by <paramref name="dt"/> seconds and returns the new reading.</summary>
        public double Tick(double dt)
        {
            if (!Enabled)
            {
                _value = 0;
                _spike = 0;
                return 0;
            }

            // Mean-reverting drift + Gaussian diffusion (inertia + natural fluctuation).
            _value += _theta * (_mean - _value) * dt + _sigma * Math.Sqrt(dt) * NextGaussian();
            if (_value < _min) _value = _min;
            if (_value > _max) _value = _max;

            // Occasional load spike, then exponential decay.
            if (_rng.NextDouble() < _spikeChancePerSecond * dt)
                _spike += _spikeMagnitude * (0.5 + _rng.NextDouble());
            _spike *= Math.Exp(-2.5 * dt);

            double reading = _value + _spike;
            double ceiling = _max * 1.35;
            if (reading > ceiling) reading = ceiling;
            if (reading < _min) reading = _min;
            return reading;
        }

        public void Reset()
        {
            _value = _mean;
            _spike = 0;
            _hasSpare = false;
        }

        private double NextGaussian()
        {
            if (_hasSpare) { _hasSpare = false; return _spare; }
            double u, v, s;
            do
            {
                u = _rng.NextDouble() * 2 - 1;
                v = _rng.NextDouble() * 2 - 1;
                s = u * u + v * v;
            } while (s >= 1 || s == 0);
            double mul = Math.Sqrt(-2.0 * Math.Log(s) / s);
            _spare = v * mul;
            _hasSpare = true;
            return u * mul;
        }
    }
}
