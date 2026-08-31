using MR200.UI.Simulation;

namespace MR200.UI.ViewModels
{
    /// <summary>
    /// Live statistics for a single sensor line (current / max / average / status),
    /// shown beside each chart and refreshed every tick.
    /// </summary>
    public sealed class SignalStatRow : BaseViewModel
    {
        public SignalStatRow(string name, string color, string unit)
        {
            Name = name;
            Color = color;
            Unit = unit;
        }

        public string Name { get; }
        public string Color { get; }   // matches the chart line colour
        public string Unit { get; }

        private string _current = "0"; public string Current { get => _current; set => SetProperty(ref _current, value); }
        private string _max = "0"; public string Max { get => _max; set => SetProperty(ref _max, value); }
        private string _average = "0"; public string Average { get => _average; set => SetProperty(ref _average, value); }
        private string _status = "Standby"; public string Status { get => _status; set => SetProperty(ref _status, value); }
        private string _statusColor = "#848283"; public string StatusColor { get => _statusColor; set => SetProperty(ref _statusColor, value); }

        public void Update(MonitoredSignal s, MachineState state)
        {
            Current = s.Current.ToString("F1");
            Max = s.Max.ToString("F1");
            Average = s.Average.ToString("F1");

            switch (state)
            {
                case MachineState.Running:
                    if (s.Online) { Status = "● Online"; StatusColor = "#10B981"; }
                    else { Status = "● Fault"; StatusColor = "#EF4444"; }
                    break;
                case MachineState.Paused:
                    Status = "❚❚ Paused"; StatusColor = "#C89B3C";
                    break;
                default:
                    Status = "○ Standby"; StatusColor = "#848283";
                    break;
            }
        }

        public void ResetStats()
        {
            Current = "0"; Max = "0"; Average = "0";
            Status = "○ Standby"; StatusColor = "#848283";
        }
    }
}
