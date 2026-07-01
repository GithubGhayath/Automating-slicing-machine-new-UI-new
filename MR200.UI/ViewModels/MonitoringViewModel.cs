using System.Collections.ObjectModel;
using System.Windows.Threading;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MR200.UI.Simulation;
using SkiaSharp;

namespace MR200.UI.ViewModels
{
    /// <summary>
    /// Drives the Machine-Monitoring dashboard. It owns the <see cref="SimulationManager"/>
    /// and a single timer; the Home page Start/Stop/End Process buttons call
    /// <see cref="Start"/>, <see cref="Pause"/> and <see cref="Reset"/>.
    ///
    /// All chart series are created once and only their point collections mutate,
    /// so LiveCharts updates incrementally without rebuilding the visual tree.
    /// </summary>
    public sealed class MonitoringViewModel : BaseViewModel
    {
        private const int MaxPoints = 140;         // bounded history (memory + smoothness)
        private const double Dt = 0.30;            // seconds per tick (calm, smooth motion)
        private const double WindowSeconds = 20;   // width of the visible sliding window

        private readonly SimulationManager _sim = new();
        private readonly DispatcherTimer _timer;

        // Each chart owns its OWN axis instances (never share an Axis across charts).
        private readonly Axis _cutX, _prodX, _convX, _feedX;

        // Point buffers (X = elapsed seconds).
        private readonly ObservableCollection<ObservablePoint> _cutA = new();
        private readonly ObservableCollection<ObservablePoint> _cutB = new();
        private readonly ObservableCollection<ObservablePoint> _prod = new();
        private readonly ObservableCollection<ObservablePoint> _convIn = new();
        private readonly ObservableCollection<ObservablePoint> _convOut = new();
        private readonly ObservableCollection<ObservablePoint>[] _feed =
        {
            new(), new(), new(), new()
        };

        public MonitoringViewModel()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _timer.Tick += (_, _) => Step();

            BuildSeries();

            // Per-chart axes. X = sliding time window; Y = fixed operating ranges
            // so the labels never reflow (which was jittering the layout).
            _cutX = MakeTimeAxis();
            _prodX = MakeTimeAxis();
            _convX = MakeTimeAxis();
            _feedX = MakeTimeAxis();

            double feed = _sim.FeedRateMetersPerMinute;
            CuttingXAxes = new[] { _cutX };
            CuttingYAxes = new[] { MakeValueAxis("Torque (N·m)", 0, 140) };
            ProductionXAxes = new[] { _prodX };
            ProductionYAxes = new[] { MakeValueAxis("Slices", 0, null) };
            ConveyorXAxes = new[] { _convX };
            ConveyorYAxes = new[] { MakeValueAxis("Speed (m/min)", feed - 4, feed + 4) };
            FeedXAxes = new[] { _feedX };
            FeedYAxes = new[] { MakeValueAxis("Torque (N·m)", 0, 60) };

            CuttingStats = new ObservableCollection<SignalStatRow>
            {
                new("Cutting Shaft A", "#E05C1A", "N·m"),
                new("Cutting Shaft B", "#17463E", "N·m"),
            };
            ConveyorStats = new ObservableCollection<SignalStatRow>
            {
                new("Input Conveyor", "#C89B3C", "m/min"),
                new("Output Conveyor", "#1F5F5B", "m/min"),
            };
            FeedStats = new ObservableCollection<SignalStatRow>
            {
                new("Feed Shaft 1", "#E05C1A", "N·m"),
                new("Feed Shaft 2", "#17463E", "N·m"),
                new("Feed Shaft 3", "#C89B3C", "N·m"),
                new("Feed Shaft 4", "#4E6E81", "N·m"),
            };

            FeedRateText = $"{_sim.FeedRateMetersPerMinute:0.#} m/min (constant)";
        }

        #region Chart series
        public ISeries[] CuttingSeries { get; private set; } = Array.Empty<ISeries>();
        public ISeries[] ProductionSeries { get; private set; } = Array.Empty<ISeries>();
        public ISeries[] ConveyorSeries { get; private set; } = Array.Empty<ISeries>();
        public ISeries[] FeedSeries { get; private set; } = Array.Empty<ISeries>();

        public Axis[] CuttingXAxes { get; }
        public Axis[] CuttingYAxes { get; }
        public Axis[] ProductionXAxes { get; }
        public Axis[] ProductionYAxes { get; }
        public Axis[] ConveyorXAxes { get; }
        public Axis[] ConveyorYAxes { get; }
        public Axis[] FeedXAxes { get; }
        public Axis[] FeedYAxes { get; }
        #endregion

        #region Live statistics
        public ObservableCollection<SignalStatRow> CuttingStats { get; }
        public ObservableCollection<SignalStatRow> ConveyorStats { get; }
        public ObservableCollection<SignalStatRow> FeedStats { get; }

        private string _prodCurrent = "0"; public string ProdCurrent { get => _prodCurrent; set => SetProperty(ref _prodCurrent, value); }
        private string _prodTotal = "0"; public string ProdTotal { get => _prodTotal; set => SetProperty(ref _prodTotal, value); }
        private string _prodAvg = "0"; public string ProdAvg { get => _prodAvg; set => SetProperty(ref _prodAvg, value); }

        private string _elapsed = "00:00:00"; public string ElapsedTime { get => _elapsed; set => SetProperty(ref _elapsed, value); }
        private string _stateText = "STANDBY"; public string StateText { get => _stateText; set => SetProperty(ref _stateText, value); }
        private string _stateColor = "#848283"; public string StateColor { get => _stateColor; set => SetProperty(ref _stateColor, value); }
        private bool _isRunning; public bool IsRunning { get => _isRunning; set => SetProperty(ref _isRunning, value); }
        public string FeedRateText { get; }
        #endregion

        #region Machine control (called from the Home page)
        public void Start()
        {
            _sim.Start();
            IsRunning = true;
            UpdateStateBadge();
            _timer.Start();
        }

        public void Pause()
        {
            _timer.Stop();
            _sim.Pause();
            IsRunning = false;
            UpdateStateBadge();
        }

        public void Reset()
        {
            _timer.Stop();
            _sim.Reset();
            IsRunning = false;

            _cutA.Clear(); _cutB.Clear(); _prod.Clear();
            _convIn.Clear(); _convOut.Clear();
            foreach (var f in _feed) f.Clear();

            foreach (var r in CuttingStats) r.ResetStats();
            foreach (var r in ConveyorStats) r.ResetStats();
            foreach (var r in FeedStats) r.ResetStats();

            ProdCurrent = "0"; ProdTotal = "0"; ProdAvg = "0";
            ElapsedTime = "00:00:00";

            // Reset the visible window back to its initial 0..window position.
            foreach (var x in new[] { _cutX, _prodX, _convX, _feedX })
            {
                x.MinLimit = 0;
                x.MaxLimit = WindowSeconds;
            }
            UpdateStateBadge();
        }
        #endregion

        private void Step()
        {
            if (!_sim.Tick(Dt)) return;
            double t = _sim.ElapsedSeconds;

            Append(_cutA, t, _sim.CuttingShaftA.Current);
            Append(_cutB, t, _sim.CuttingShaftB.Current);
            Append(_prod, t, _sim.Production.Total);
            Append(_convIn, t, _sim.InputConveyor.Current);
            Append(_convOut, t, _sim.OutputConveyor.Current);
            for (int i = 0; i < 4; i++) Append(_feed[i], t, _sim.FeedShafts[i].Current);

            CuttingStats[0].Update(_sim.CuttingShaftA, _sim.State);
            CuttingStats[1].Update(_sim.CuttingShaftB, _sim.State);
            ConveyorStats[0].Update(_sim.InputConveyor, _sim.State);
            ConveyorStats[1].Update(_sim.OutputConveyor, _sim.State);
            for (int i = 0; i < 4; i++) FeedStats[i].Update(_sim.FeedShafts[i], _sim.State);

            ProdCurrent = _sim.Production.CurrentRate.ToString("F1");
            ProdTotal = _sim.Production.Total.ToString("F0");
            ProdAvg = _sim.Production.AverageRate.ToString("F1");

            ElapsedTime = TimeSpan.FromSeconds(_sim.ElapsedSeconds).ToString(@"hh\:mm\:ss");

            SlideWindow(t);
        }

        // Slides every chart's X window forward so the charts scroll by themselves.
        private void SlideWindow(double t)
        {
            double max = Math.Max(t, WindowSeconds);
            double min = max - WindowSeconds;
            foreach (var x in new[] { _cutX, _prodX, _convX, _feedX })
            {
                x.MinLimit = min;
                x.MaxLimit = max;
            }
        }

        private static void Append(ObservableCollection<ObservablePoint> buffer, double x, double y)
        {
            buffer.Add(new ObservablePoint(x, y));
            if (buffer.Count > MaxPoints) buffer.RemoveAt(0);
        }

        private void UpdateStateBadge()
        {
            switch (_sim.State)
            {
                case MachineState.Running: StateText = "RUNNING"; StateColor = "#10B981"; break;
                case MachineState.Paused: StateText = "PAUSED"; StateColor = "#C89B3C"; break;
                default: StateText = "STANDBY"; StateColor = "#848283"; break;
            }
        }

        private static LineSeries<ObservablePoint> Line(string name, string hex, ObservableCollection<ObservablePoint> values, bool fill = false, float thickness = 2.5f)
        {
            var color = SKColor.Parse(hex);
            return new LineSeries<ObservablePoint>
            {
                Name = name,
                Values = values,
                Stroke = new SolidColorPaint(color, thickness),
                Fill = fill ? new SolidColorPaint(color.WithAlpha(45)) : null,
                GeometrySize = 0,
                LineSmoothness = 0.6,                              // softer curves
                AnimationsSpeed = TimeSpan.FromMilliseconds(320)   // gentle, slower glide
            };
        }

        // A time (X) axis for a single chart. Starts at 0..window and is slid forward
        // each tick. A forced 4-second step keeps the tick labels perfectly stable.
        private static Axis MakeTimeAxis() => new Axis
        {
            MinLimit = 0,
            MaxLimit = WindowSeconds,
            MinStep = 4,
            ForceStepToMin = true,
            Labeler = v => v.ToString("F0"),
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#9AA3AD")),
            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#EEF1F4")) { StrokeThickness = 1 }
        };

        // A value (Y) axis with a fixed operating range so labels never reflow.
        private static Axis MakeValueAxis(string name, double? min, double? max) => new Axis
        {
            Name = name,
            NamePaint = new SolidColorPaint(SKColor.Parse("#6B7280")),
            NameTextSize = 12,
            MinLimit = min,
            MaxLimit = max,
            TextSize = 11,
            LabelsPaint = new SolidColorPaint(SKColor.Parse("#9AA3AD")),
            SeparatorsPaint = new SolidColorPaint(SKColor.Parse("#EEF1F4")) { StrokeThickness = 1 }
        };

        private void BuildSeries()
        {
            CuttingSeries = new ISeries[]
            {
                Line("Cutting Shaft A", "#E05C1A", _cutA),
                Line("Cutting Shaft B", "#17463E", _cutB),
            };
            ProductionSeries = new ISeries[]
            {
                Line("Total Production", "#4E6E81", _prod, fill: true),
            };
            ConveyorSeries = new ISeries[]
            {
                Line("Input Conveyor", "#C89B3C", _convIn),
                Line("Output Conveyor", "#1F5F5B", _convOut),
            };
            FeedSeries = new ISeries[]
            {
                Line("Feed Shaft 1", "#E05C1A", _feed[0], thickness: 2f),
                Line("Feed Shaft 2", "#17463E", _feed[1], thickness: 2f),
                Line("Feed Shaft 3", "#C89B3C", _feed[2], thickness: 2f),
                Line("Feed Shaft 4", "#4E6E81", _feed[3], thickness: 2f),
            };
        }
    }
}
