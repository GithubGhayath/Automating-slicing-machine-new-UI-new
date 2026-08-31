namespace MR200.UI.Simulation
{
    /// <summary>
    /// Wraps a <see cref="SensorSignal"/> and tracks the live statistics every
    /// chart needs (current / maximum / average) plus an online flag.
    /// </summary>
    public sealed class MonitoredSignal
    {
        private readonly SensorSignal _signal;
        private double _sum;
        private long _count;

        public MonitoredSignal(string name, SensorSignal signal)
        {
            Name = name;
            _signal = signal;
        }

        public string Name { get; }
        public double Current { get; private set; }
        public double Max { get; private set; }
        public double Average => _count > 0 ? _sum / _count : 0;
        public bool Online => _signal.Enabled;

        public double Tick(double dt)
        {
            Current = _signal.Tick(dt);
            if (Current > Max) Max = Current;
            _sum += Current;
            _count++;
            return Current;
        }

        public void Reset()
        {
            _signal.Reset();
            Current = 0;
            Max = 0;
            _sum = 0;
            _count = 0;
        }
    }
}
