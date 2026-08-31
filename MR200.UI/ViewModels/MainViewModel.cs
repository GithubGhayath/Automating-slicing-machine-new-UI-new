using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using DataAccess.Entities;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using MR200.UI.Database;
using MR200.UI.Database.Maintenance;
using MR200.UI.Database.Utility;
using MR200.UI.Database.Wood;
using MR200.UI.Helpers;
using MR200.UI.MaintenanceSystem;
using SkiaSharp;

namespace MR200.UI.ViewModels
{
    public class ForceRow
    {
        public double Theta { get; set; }
        public double CuttingForce { get; set; }
        public double ActiveForce { get; set; }
        public double FrictionForce { get; set; }
        public double ThrustForce { get; set; }
        public double ShearForce { get; set; }
        public double NormalShear { get; set; }
        public double NormalRake { get; set; }
        public double CuttingForceMoment { get; set; }
    }

    public class AngleChipRow
    {
        public double Angle { get; set; }
        public double ChipThickness { get; set; }
    }

    public class HistoryRow
    {
        public int ProcessNo { get; set; }
        public string WoodType { get; set; } = "";
        public string ProductDimension { get; set; } = "";
        public double ProductionVolume { get; set; }
        public double TotalFees { get; set; }
        public double ConsumedElectricity { get; set; }
        public string StartAt { get; set; } = "";
        public string EndAt { get; set; } = "";
    }

    // Current health of one monitored physical element, for the Maintenance screen.
    public class ElementHealthRow
    {
        public int ElementId { get; set; }
        public int Order { get; set; }
        public string Element { get; set; } = "";
        public string ElementType { get; set; } = "";
        public string Position { get; set; } = "";
        public string ConsumedLife { get; set; } = "";
        public string DefaultLife { get; set; } = "";
        public string RemainingLife { get; set; } = "";
        public string LifeUsed { get; set; } = "";
        public double LifeUsedPercent { get; set; }
        public string Status { get; set; } = "Normal";
        public string StatusColor { get; set; } = "#10B981";
        public string LastMaintenance { get; set; } = "Never";
        public string Price { get; set; } = "";
    }

    // One historical maintenance operation, for the Maintenance screen table.
    public class MaintenanceHistoryRow
    {
        public int MaintenanceId { get; set; }
        public int Order { get; set; }
        public string Element { get; set; } = "";
        public string ElementType { get; set; } = "";
        public string Position { get; set; } = "";
        public string MaintenanceDate { get; set; } = "";
        public string DoneBy { get; set; } = "";
        public string Cost { get; set; } = "";
        public string ElementPrice { get; set; } = "";
        public string StoppingTimeCost { get; set; } = "";
        public string TotalCost { get; set; } = "";
    }

    // One animated force bar inside the inline process-detail panel.
    public class ForceBar
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
        public double Percent { get; set; }          // 0..100 relative to the largest force
        public string BarColor { get; set; } = "#E05C1A";
    }

    public class MainViewModel : BaseViewModel
    {
        private readonly DispatcherTimer _timer;
        private DateTime _machineStartsAt;

        // True from the first Start of a production run until End Process. Lets Start
        // tell a resume-after-Stop apart from the beginning of a new run.
        private bool _runInProgress;

        // When the machine was last stopped, so a resume can discount the paused span.
        private DateTime _pausedAt;

        private int _timerMs;
        private readonly Random _rand = new();
        private double _t;

        private double _FeedVelocity, _MaxCuttingVelocity, _CoefficientOfFriction;
        private double _DepthOfCutWoodInMeter, _TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWoodInMeter;
        private double _RakeAngleInDegrees, _BladeDiameter, _KerfThicknessInMeter;
        private int _NumberOfTooth, _NumberOfBlades;
        private double _VolumetricProductionRateMeter3Hour;

        private double _FeedPerTeethInMeterPerTeeth, _NumberOfRotationsInRPM;
        private double _FrictionAngleInDegrees, _ShearAngleInDegrees;
        private double _FrictionCorrectionCoefficient, _ShearingStrainAlongShearPlane;
        private double _EnterAngleInDegrees, _ExitAngleInDegrees, _CenterAngleOfCuttingInDegrees;
        private double _TheMeanChipThicknessInMeter;
        private double _CuttingForceInNewton, _ActiveForceInNewton, _FrictionForceOnRakeInNewton;
        private double _ThrustForceInNewton, _ShearForceInNewton;
        private double _NormalForceToShearPlaneInNewton, _NormalForceToRakeInNewton;
        private double _ShearYieldStress, _SpecificWorkToSurfaceSeparationInJoulPerMeter2;
        private List<double> _StudiedAngles = new();
        private Dictionary<double, double> _ChipThicknessAtStudiedAngles = new();
        private WoodType? _SelectedWood;

        private readonly ObservableCollection<ObservablePoint> _torquePoints = new();
        private readonly ObservableCollection<ObservablePoint> _productionPoints = new();
        private DateTime _processStartTime;

        // Machine-monitoring dashboard (driven by the same Start/Stop/End controls).
        public MonitoringViewModel Monitoring { get; } = new();

        // Predictive maintenance: tracks the life every monitored element consumes
        // while the machine runs.
        private readonly ElementLifeMonitoringService _lifeMonitor = new();
        private bool _isHandlingFailure;

        public MainViewModel()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _timer.Tick += Timer_Tick;

            WoodTypes = new ObservableCollection<string> { "Select Wood Type" };
            ForceRows = new ObservableCollection<ForceRow>();
            AngleChipRows = new ObservableCollection<AngleChipRow>();
            HistoryRows = new ObservableCollection<HistoryRow>();
            HistoryList = new List<OperationsProcess>();

            NavigateCommand = new RelayCommand(Navigate);
            CalculateForcesCommand = new RelayCommand(_ => CalculateForces(), _ => CanCalculate);
            StartMachineCommand = new RelayCommand(_ => StartMachine(), _ => CanStart);
            StopMachineCommand = new RelayCommand(_ => StopMachine(), _ => CanStop);
            EndProcessCommand = new RelayCommand(_ => EndProcess(), _ => CanEndProcess);
            ViewDetailsCommand = new RelayCommand(ViewDetails);
            ExportPdfCommand = new RelayCommand(ExportPdf);
            CloseDetailCommand = new RelayCommand(_ => IsDetailVisible = false);
            ExportSelectedPdfCommand = new RelayCommand(_ => ExportPdf(DetailProcessNo));

            InitCuttingForceChart();
            InitMomentChart();
            InitTorqueChart();
            InitProductionChart();

            HistoryProductionSeries = Array.Empty<ISeries>();
            WoodTypePieSeries = Array.Empty<ISeries>();

            // ---- Maintenance system ----
            ElementHealthRows = new ObservableCollection<ElementHealthRow>();
            MaintenanceRows = new ObservableCollection<MaintenanceHistoryRow>();

            RecordMaintenanceCommand = new RelayCommand(OpenMaintenanceForm);
            SaveMaintenanceCommand = new RelayCommand(_ => SaveMaintenance(), _ => CanSaveMaintenance);
            CancelMaintenanceCommand = new RelayCommand(_ => IsMaintenanceFormVisible = false);
            RefreshMaintenanceCommand = new RelayCommand(_ => LoadMaintenance());
            DismissFailureAlertCommand = new RelayCommand(_ => IsFailureAlertVisible = false);
            GoToMaintenanceFromAlertCommand = new RelayCommand(_ =>
            {
                IsFailureAlertVisible = false;
                CurrentPage = "Maintenance";
                LoadMaintenance();
            });

            _lifeMonitor.ElementFailed += OnElementFailed;
            _lifeMonitor.MonitoringError += (_, ex) => MaintenanceStatusMessage = "Maintenance system: " + ex.Message;

            LoadWoodTypes();
        }

        #region Navigation
        private string _currentPage = "Home";
        public string CurrentPage { get => _currentPage; set => SetProperty(ref _currentPage, value); }
        #endregion

        #region Wood Selection
        public ObservableCollection<string> WoodTypes { get; }
        private int _selectedWoodIndex;
        public int SelectedWoodIndex
        {
            get => _selectedWoodIndex;
            set { if (SetProperty(ref _selectedWoodIndex, value)) OnWoodTypeChanged(); }
        }
        #endregion

        #region Display Properties
        private string _feedVelocity = "[N/A]"; public string FeedVelocityDisplay { get => _feedVelocity; set => SetProperty(ref _feedVelocity, value); }
        private string _maxCuttingVelocity = "[N/A]"; public string MaxCuttingVelocityDisplay { get => _maxCuttingVelocity; set => SetProperty(ref _maxCuttingVelocity, value); }
        private string _feedPerTeeth = "[N/A]"; public string FeedPerTeethDisplay { get => _feedPerTeeth; set => SetProperty(ref _feedPerTeeth, value); }
        private string _numberOfRotations = "[N/A]"; public string NumberOfRotationsDisplay { get => _numberOfRotations; set => SetProperty(ref _numberOfRotations, value); }
        private string _frictionAngle = "[N/A]"; public string FrictionAngleDisplay { get => _frictionAngle; set => SetProperty(ref _frictionAngle, value); }
        private string _shearAngle = "[N/A]"; public string ShearAngleDisplay { get => _shearAngle; set => SetProperty(ref _shearAngle, value); }
        private string _frictionCorrCoeff = "[N/A]"; public string FrictionCorrCoeffDisplay { get => _frictionCorrCoeff; set => SetProperty(ref _frictionCorrCoeff, value); }
        private string _shearingStrain = "[N/A]"; public string ShearingStrainDisplay { get => _shearingStrain; set => SetProperty(ref _shearingStrain, value); }
        private string _enterAngle = "[N/A]"; public string EnterAngleDisplay { get => _enterAngle; set => SetProperty(ref _enterAngle, value); }
        private string _exitAngle = "[N/A]"; public string ExitAngleDisplay { get => _exitAngle; set => SetProperty(ref _exitAngle, value); }
        private string _centerCuttingAngle = "[N/A]"; public string CenterCuttingAngleDisplay { get => _centerCuttingAngle; set => SetProperty(ref _centerCuttingAngle, value); }
        private string _meanChipThickness = "[N/A]"; public string MeanChipThicknessDisplay { get => _meanChipThickness; set => SetProperty(ref _meanChipThickness, value); }
        private string _numberOfTeeth = "[N/A]"; public string NumberOfTeethDisplay { get => _numberOfTeeth; set => SetProperty(ref _numberOfTeeth, value); }
        private string _numberOfBladesDisplay = "[N/A]"; public string NumberOfBladesDisplay { get => _numberOfBladesDisplay; set => SetProperty(ref _numberOfBladesDisplay, value); }
        private string _volumetricRate = "[N/A]"; public string VolumetricRateDisplay { get => _volumetricRate; set => SetProperty(ref _volumetricRate, value); }
        private string _cuttingForceDisplay = "[N/A]"; public string CuttingForceDisplay { get => _cuttingForceDisplay; set => SetProperty(ref _cuttingForceDisplay, value); }
        private string _activeForceDisplay = "[N/A]"; public string ActiveForceDisplay { get => _activeForceDisplay; set => SetProperty(ref _activeForceDisplay, value); }
        private string _thrustForceDisplay = "[N/A]"; public string ThrustForceDisplay { get => _thrustForceDisplay; set => SetProperty(ref _thrustForceDisplay, value); }
        private string _shearForceDisplay = "[N/A]"; public string ShearForceDisplay { get => _shearForceDisplay; set => SetProperty(ref _shearForceDisplay, value); }
        private string _frictionForceRake = "[N/A]"; public string FrictionForceRakeDisplay { get => _frictionForceRake; set => SetProperty(ref _frictionForceRake, value); }
        private string _normalShear = "[N/A]"; public string NormalShearDisplay { get => _normalShear; set => SetProperty(ref _normalShear, value); }
        private string _normalRake = "[N/A]"; public string NormalRakeDisplay { get => _normalRake; set => SetProperty(ref _normalRake, value); }
        private string _shearYieldStress = "[N/A]"; public string ShearYieldStressDisplay { get => _shearYieldStress; set => SetProperty(ref _shearYieldStress, value); }
        private string _specificWork = "[N/A]"; public string SpecificWorkDisplay { get => _specificWork; set => SetProperty(ref _specificWork, value); }
        private string _coeffFriction = "[N/A]"; public string CoeffFrictionDisplay { get => _coeffFriction; set => SetProperty(ref _coeffFriction, value); }
        private string _cuttingForceFunc = "[N/A]"; public string CuttingForceFuncDisplay { get => _cuttingForceFunc; set => SetProperty(ref _cuttingForceFunc, value); }
        private string _shaftTorqueFunc = "[N/A]"; public string ShaftTorqueFuncDisplay { get => _shaftTorqueFunc; set => SetProperty(ref _shaftTorqueFunc, value); }
        private string _maxShaftTorque = "[N/A]"; public string MaxShaftTorqueDisplay { get => _maxShaftTorque; set => SetProperty(ref _maxShaftTorque, value); }
        private string _timeCounter = "00:00:00"; public string TimeCounter { get => _timeCounter; set => SetProperty(ref _timeCounter, value); }
        #endregion

        #region History Dashboard Properties
        private string _totalFeesCard = "$0.00"; public string TotalFeesCard { get => _totalFeesCard; set => SetProperty(ref _totalFeesCard, value); }
        private string _consumedEnergyCard = "0.00 KWh"; public string ConsumedEnergyCard { get => _consumedEnergyCard; set => SetProperty(ref _consumedEnergyCard, value); }
        private string _productionVolumeCard = "0.00 M³"; public string ProductionVolumeCard { get => _productionVolumeCard; set => SetProperty(ref _productionVolumeCard, value); }
        private string _totalProcessesCard = "0"; public string TotalProcessesCard { get => _totalProcessesCard; set => SetProperty(ref _totalProcessesCard, value); }
        private string _avgCuttingForceCard = "0.00"; public string AvgCuttingForceCard { get => _avgCuttingForceCard; set => SetProperty(ref _avgCuttingForceCard, value); }
        private string _maxCuttingForceCard = "0.00"; public string MaxCuttingForceCard { get => _maxCuttingForceCard; set => SetProperty(ref _maxCuttingForceCard, value); }
        private string _avgCuttingMomentCard = "0.00"; public string AvgCuttingMomentCard { get => _avgCuttingMomentCard; set => SetProperty(ref _avgCuttingMomentCard, value); }
        private string _maxCuttingMomentCard = "0.00"; public string MaxCuttingMomentCard { get => _maxCuttingMomentCard; set => SetProperty(ref _maxCuttingMomentCard, value); }
        private string _energyEfficiencyCard = "0.000"; public string EnergyEfficiencyCard { get => _energyEfficiencyCard; set => SetProperty(ref _energyEfficiencyCard, value); }
        private string _avgCostPerM3Card = "$0.00"; public string AvgCostPerM3Card { get => _avgCostPerM3Card; set => SetProperty(ref _avgCostPerM3Card, value); }
        private string _hardwoodCount = "0"; public string HardwoodCount { get => _hardwoodCount; set => SetProperty(ref _hardwoodCount, value); }
        private string _softwoodCount = "0"; public string SoftwoodCount { get => _softwoodCount; set => SetProperty(ref _softwoodCount, value); }

        // Period captions describing the time span / scope each card is aggregated over.
        private string _periodRangeText = "No data yet"; public string PeriodRangeText { get => _periodRangeText; set => SetProperty(ref _periodRangeText, value); }
        private string _allProcessesScopeText = "All processes"; public string AllProcessesScopeText { get => _allProcessesScopeText; set => SetProperty(ref _allProcessesScopeText, value); }
        #endregion

        #region Inline Process Detail Panel
        private bool _isDetailVisible; public bool IsDetailVisible { get => _isDetailVisible; set => SetProperty(ref _isDetailVisible, value); }
        private string _detailTitle = ""; public string DetailTitle { get => _detailTitle; set => SetProperty(ref _detailTitle, value); }
        private string _detailSubtitle = ""; public string DetailSubtitle { get => _detailSubtitle; set => SetProperty(ref _detailSubtitle, value); }
        private string _detailCuttingSpeed = ""; public string DetailCuttingSpeed { get => _detailCuttingSpeed; set => SetProperty(ref _detailCuttingSpeed, value); }
        private string _detailFeedRate = ""; public string DetailFeedRate { get => _detailFeedRate; set => SetProperty(ref _detailFeedRate, value); }
        private string _detailShaftSpeed = ""; public string DetailShaftSpeed { get => _detailShaftSpeed; set => SetProperty(ref _detailShaftSpeed, value); }
        private string _detailEnergy = ""; public string DetailEnergy { get => _detailEnergy; set => SetProperty(ref _detailEnergy, value); }
        private string _detailFrictionAngle = ""; public string DetailFrictionAngle { get => _detailFrictionAngle; set => SetProperty(ref _detailFrictionAngle, value); }
        private string _detailShearAngle = ""; public string DetailShearAngle { get => _detailShearAngle; set => SetProperty(ref _detailShearAngle, value); }
        private string _detailCenterAngle = ""; public string DetailCenterAngle { get => _detailCenterAngle; set => SetProperty(ref _detailCenterAngle, value); }
        private string _detailMoment = ""; public string DetailMoment { get => _detailMoment; set => SetProperty(ref _detailMoment, value); }
        private int _detailProcessNo; public int DetailProcessNo { get => _detailProcessNo; set => SetProperty(ref _detailProcessNo, value); }
        public ObservableCollection<ForceBar> SelectedForceBars { get; } = new();
        #endregion

        #region State
        private bool _canCalculate; public bool CanCalculate { get => _canCalculate; set { SetProperty(ref _canCalculate, value); CommandManager.InvalidateRequerySuggested(); } }
        private bool _canStart; public bool CanStart { get => _canStart; set { SetProperty(ref _canStart, value); CommandManager.InvalidateRequerySuggested(); } }
        private bool _canStop; public bool CanStop { get => _canStop; set { SetProperty(ref _canStop, value); CommandManager.InvalidateRequerySuggested(); } }
        private bool _canEndProcess; public bool CanEndProcess { get => _canEndProcess; set { SetProperty(ref _canEndProcess, value); CommandManager.InvalidateRequerySuggested(); } }
        #endregion

        #region Collections
        public ObservableCollection<ForceRow> ForceRows { get; }
        public ObservableCollection<AngleChipRow> AngleChipRows { get; }
        public ObservableCollection<HistoryRow> HistoryRows { get; }
        public List<OperationsProcess> HistoryList { get; set; }
        #endregion

        #region Chart Series
        public ISeries[] CuttingForceSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] MomentSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] TorqueSeries { get; set; } = Array.Empty<ISeries>();
        public ISeries[] ProductionSeries { get; set; } = Array.Empty<ISeries>();
        private ISeries[] _historyProductionSeries = Array.Empty<ISeries>();
        public ISeries[] HistoryProductionSeries { get => _historyProductionSeries; set => SetProperty(ref _historyProductionSeries, value); }
        private ISeries[] _woodTypePieSeries = Array.Empty<ISeries>();
        public ISeries[] WoodTypePieSeries { get => _woodTypePieSeries; set => SetProperty(ref _woodTypePieSeries, value); }

        // NEW: force-component comparison columns across recent processes
        private ISeries[] _forceComparisonSeries = Array.Empty<ISeries>();
        public ISeries[] ForceComparisonSeries { get => _forceComparisonSeries; set => SetProperty(ref _forceComparisonSeries, value); }
        private Axis[] _forceComparisonXAxes = Array.Empty<Axis>();
        public Axis[] ForceComparisonXAxes { get => _forceComparisonXAxes; set => SetProperty(ref _forceComparisonXAxes, value); }

        // NEW: radar/polar force profile of the latest (or selected) process
        private ISeries[] _forceRadarSeries = Array.Empty<ISeries>();
        public ISeries[] ForceRadarSeries { get => _forceRadarSeries; set => SetProperty(ref _forceRadarSeries, value); }
        private PolarAxis[] _forceRadarAngleAxes = Array.Empty<PolarAxis>();
        public PolarAxis[] ForceRadarAngleAxes { get => _forceRadarAngleAxes; set => SetProperty(ref _forceRadarAngleAxes, value); }
        #endregion

        #region Commands
        public RelayCommand NavigateCommand { get; }
        public RelayCommand CalculateForcesCommand { get; }
        public RelayCommand StartMachineCommand { get; }
        public RelayCommand StopMachineCommand { get; }
        public RelayCommand EndProcessCommand { get; }
        public RelayCommand ViewDetailsCommand { get; }
        public RelayCommand ExportPdfCommand { get; }
        public RelayCommand CloseDetailCommand { get; }
        public RelayCommand ExportSelectedPdfCommand { get; }
        #endregion

        private void LoadWoodTypes()
        {
            try
            {
                var types = WoodCRUD.GetWoodList();
                foreach (var type in types) WoodTypes.Add(type.Type);
            }
            catch { }
        }

        private void Navigate(object? page)
        {
            if (page is string p)
            {
                CurrentPage = p;
                if (p == "History") LoadHistory();
                else if (p == "Maintenance") LoadMaintenance();
            }
        }

        private void OnWoodTypeChanged()
        {
            if (_selectedWoodIndex > 0)
            {
                try
                {
                    _SelectedWood = WoodCRUD.GetWoodByName(WoodTypes[_selectedWoodIndex]);
                    _ShearYieldStress = _SelectedWood.ShearYieldStressInMpa;
                    _SpecificWorkToSurfaceSeparationInJoulPerMeter2 = _SelectedWood.SpecificWorkToSurfaceSeparationJoulPerMeter2;
                    _CoefficientOfFriction = _SelectedWood.CoefficientOfFriction;
                    ShearYieldStressDisplay = _ShearYieldStress.ToString();
                    SpecificWorkDisplay = _SpecificWorkToSurfaceSeparationInJoulPerMeter2.ToString();
                    CoeffFrictionDisplay = _CoefficientOfFriction.ToString();
                    CanCalculate = true;
                }
                catch { CanCalculate = false; }
            }
            else { CanCalculate = false; CanStart = false; CanStop = false; }
        }

        #region Calculations (identical to existing project equations)
        private void FillPropertiesValue()
        {
            _FeedVelocity = Convert.ToDouble(clsHelper.ReadFromConfiguration("FeedVelocity"));
            _NumberOfBlades = Convert.ToInt32(clsHelper.ReadFromConfiguration("NumberOfBlades"));
            _MaxCuttingVelocity = clsHelper.MeterPerSecToMeterPerMin(Convert.ToDouble(clsHelper.ReadFromConfiguration("MaxCuttingVelocity")));
            _DepthOfCutWoodInMeter = clsHelper.MillimeterToMeter(Convert.ToDouble(clsHelper.ReadFromConfiguration("DepthOfCutWood")));
            _TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWoodInMeter = clsHelper.MillimeterToMeter(Convert.ToDouble(clsHelper.ReadFromConfiguration("TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWood")));
            _RakeAngleInDegrees = Convert.ToDouble(clsHelper.ReadFromConfiguration("RakeAngle"));
            _NumberOfTooth = Convert.ToInt32(clsHelper.ReadFromConfiguration("NumberOfTooth"));
            _BladeDiameter = clsHelper.MillimeterToMeter(Convert.ToDouble(clsHelper.ReadFromConfiguration("BladeDiameter")));
            _KerfThicknessInMeter = clsHelper.MillimeterToMeter(Convert.ToDouble(clsHelper.ReadFromConfiguration("KerfThickness")));
        }

        private void PerformCalculations()
        {
            _FeedPerTeethInMeterPerTeeth = clsMainEquations.FeedPerTeeth_Unit_MeterPerTeeth(_MaxCuttingVelocity, _FeedVelocity, _BladeDiameter, _NumberOfTooth);
            _NumberOfRotationsInRPM = clsMainEquations.NumberOfRotations_Unit_RPM(_FeedVelocity, _FeedPerTeethInMeterPerTeeth, _NumberOfTooth);
            _FrictionAngleInDegrees = clsMainEquations.FrictionAngle_Unit_Degrees(_CoefficientOfFriction);
            _ShearAngleInDegrees = clsMainEquations.ShearAngle_Unit_Degrees(clsHelper.DegreesToRadians(_FrictionAngleInDegrees), clsHelper.DegreesToRadians(_RakeAngleInDegrees));
            _FrictionCorrectionCoefficient = clsMainEquations.FrictionCorrectionCoefficient_Unit_None(clsHelper.DegreesToRadians(_FrictionAngleInDegrees), clsHelper.DegreesToRadians(_ShearAngleInDegrees), clsHelper.DegreesToRadians(_RakeAngleInDegrees));
            _ShearingStrainAlongShearPlane = clsMainEquations.ShearingStrainAlongShearPlane(clsHelper.DegreesToRadians(_ShearAngleInDegrees), clsHelper.DegreesToRadians(_RakeAngleInDegrees));
            _EnterAngleInDegrees = clsMainEquations.EnterAngle_Unit_Degrees(_DepthOfCutWoodInMeter, _TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWoodInMeter, _BladeDiameter / 2);
            _ExitAngleInDegrees = clsMainEquations.ExitAngle_Unit_Degrees(_TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWoodInMeter, _BladeDiameter / 2);
            _CenterAngleOfCuttingInDegrees = clsMainEquations.CenterAngleOfCutting(_DepthOfCutWoodInMeter, _TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWoodInMeter, _BladeDiameter / 2);
            _TheMeanChipThicknessInMeter = clsMainEquations.TheMeanChipThickness_Unit_Meter(_CenterAngleOfCuttingInDegrees, _FeedPerTeethInMeterPerTeeth);
            _StudiedAngles = clsMainEquations.GetStudiedAngles(_DepthOfCutWoodInMeter, _TheDistanceBetweenTheCenterOfTheDiscAndTheLowestPointOfTheWoodInMeter, _BladeDiameter / 2, _NumberOfTooth);
            _ChipThicknessAtStudiedAngles = clsMainEquations.ChipThicknessAtStudiedAngles(_StudiedAngles, _FeedPerTeethInMeterPerTeeth);
            _VolumetricProductionRateMeter3Hour = clsMainEquations.VolumetricProductionRateMeter3PerHour(clsHelper.MeterPerMinToMeterPerSec(_FeedVelocity), (0.03 * 0.2));
        }

        private void DisplayResults()
        {
            FeedVelocityDisplay = _FeedVelocity.ToString();
            MaxCuttingVelocityDisplay = _MaxCuttingVelocity.ToString();
            CoeffFrictionDisplay = _CoefficientOfFriction.ToString();
            FeedPerTeethDisplay = _FeedPerTeethInMeterPerTeeth.ToString();
            NumberOfRotationsDisplay = _NumberOfRotationsInRPM.ToString();
            FrictionAngleDisplay = _FrictionAngleInDegrees.ToString();
            ShearAngleDisplay = _ShearAngleInDegrees.ToString();
            FrictionCorrCoeffDisplay = _FrictionCorrectionCoefficient.ToString();
            ShearingStrainDisplay = _ShearingStrainAlongShearPlane.ToString();
            EnterAngleDisplay = _EnterAngleInDegrees.ToString();
            ExitAngleDisplay = _ExitAngleInDegrees.ToString();
            CenterCuttingAngleDisplay = _CenterAngleOfCuttingInDegrees.ToString();
            MeanChipThicknessDisplay = _TheMeanChipThicknessInMeter.ToString();
            NumberOfTeethDisplay = (_StudiedAngles.Count - 1).ToString();
            NumberOfBladesDisplay = _NumberOfBlades.ToString();
            VolumetricRateDisplay = _VolumetricProductionRateMeter3Hour.ToString("F4");

            AngleChipRows.Clear();
            foreach (var kvp in _ChipThicknessAtStudiedAngles)
                AngleChipRows.Add(new AngleChipRow { Angle = kvp.Key, ChipThickness = kvp.Value });
        }

        private void CalculateForces()
        {
            FillPropertiesValue();
            PerformCalculations();
            DisplayResults();

            _CuttingForceInNewton = clsMainEquations.CuttingForce_Unit_Newton(_ShearYieldStress, _KerfThicknessInMeter, _ShearingStrainAlongShearPlane, _FrictionCorrectionCoefficient, _TheMeanChipThicknessInMeter, _SpecificWorkToSurfaceSeparationInJoulPerMeter2);
            _ActiveForceInNewton = clsMainEquations.ActiveForce_Unit_Newton(_CuttingForceInNewton);
            _ThrustForceInNewton = clsMainEquations.ThrustForce_Unit_Newton(_CuttingForceInNewton);
            _ShearForceInNewton = clsMainEquations.ShearForce_Unit_Newton(clsHelper.DegreesToRadians(_ShearAngleInDegrees), _CuttingForceInNewton);
            _NormalForceToShearPlaneInNewton = clsMainEquations.NormalForceToShearPlane_Unit_Newton(clsHelper.DegreesToRadians(_ShearAngleInDegrees), _CuttingForceInNewton);
            _NormalForceToRakeInNewton = clsMainEquations.NormalForceToRake_Unit_Newton(clsHelper.DegreesToRadians(_FrictionAngleInDegrees), _CuttingForceInNewton);
            _FrictionForceOnRakeInNewton = clsMainEquations.FrictionForceOnRake_Unit_Newton(clsHelper.DegreesToRadians(_FrictionAngleInDegrees), _CuttingForceInNewton);

            CuttingForceDisplay = _CuttingForceInNewton.ToString();
            ActiveForceDisplay = _ActiveForceInNewton.ToString();
            ThrustForceDisplay = _ThrustForceInNewton.ToString();
            ShearForceDisplay = _ShearForceInNewton.ToString();
            FrictionForceRakeDisplay = _FrictionForceOnRakeInNewton.ToString();
            NormalShearDisplay = _NormalForceToShearPlaneInNewton.ToString();
            NormalRakeDisplay = _NormalForceToRakeInNewton.ToString();

            double forceSlope = (_ShearYieldStress * 1000000 * _KerfThicknessInMeter * _ShearingStrainAlongShearPlane) / _FrictionCorrectionCoefficient;
            double forceIntercept = _SpecificWorkToSurfaceSeparationInJoulPerMeter2 * _KerfThicknessInMeter / _FrictionCorrectionCoefficient;
            double momentSlope = forceSlope * _NumberOfBlades * (_BladeDiameter / 2);
            double momentIntercept = forceIntercept * _NumberOfBlades * (_BladeDiameter / 2);

            CuttingForceFuncDisplay = $"{forceSlope:F2} hm + {forceIntercept:F4}";
            ShaftTorqueFuncDisplay = $"{momentSlope:F2} hm + {momentIntercept:F4}";

            UpdateCuttingForceChart(forceSlope, forceIntercept, _ChipThicknessAtStudiedAngles.Values.First());
            UpdateMomentChart(momentSlope, momentIntercept, _ChipThicknessAtStudiedAngles.Values.First());
            FillForceDataGrid();
            CanStart = true;
        }

        private void FillForceDataGrid()
        {
            ForceRows.Clear();
            double maxMoment = 0;
            for (int i = 0; i < _ChipThicknessAtStudiedAngles.Count; i++)
            {
                double chip = _ChipThicknessAtStudiedAngles.Values.ToList()[i];
                double cf = clsMainEquations.CuttingForce_Unit_Newton(_ShearYieldStress, _KerfThicknessInMeter, _ShearingStrainAlongShearPlane, _FrictionCorrectionCoefficient, chip, _SpecificWorkToSurfaceSeparationInJoulPerMeter2);
                double af = clsMainEquations.ActiveForce_Unit_Newton(cf);
                double tf = clsMainEquations.ThrustForce_Unit_Newton(cf);
                double sf = clsMainEquations.ShearForce_Unit_Newton(clsHelper.DegreesToRadians(_ShearAngleInDegrees), cf);
                double ns = clsMainEquations.NormalForceToShearPlane_Unit_Newton(clsHelper.DegreesToRadians(_ShearAngleInDegrees), cf);
                double nr = clsMainEquations.NormalForceToRake_Unit_Newton(clsHelper.DegreesToRadians(_FrictionAngleInDegrees), cf);
                double ff = clsMainEquations.FrictionForceOnRake_Unit_Newton(clsHelper.DegreesToRadians(_FrictionAngleInDegrees), cf);
                double moment = clsMainEquations.MomentOfCuttingForce_Unit_NewtonMeter(cf, _BladeDiameter / 2);

                ForceRows.Add(new ForceRow { Theta = _StudiedAngles[i], CuttingForce = cf, ActiveForce = af, FrictionForce = ff, ThrustForce = tf, ShearForce = sf, NormalShear = ns, NormalRake = nr, CuttingForceMoment = moment });
                if (moment > maxMoment) maxMoment = moment;
            }
            MaxShaftTorqueDisplay = (maxMoment * _NumberOfBlades).ToString();
        }
        #endregion

        #region Machine Control
        private void StartMachine()
        {
            if (_runInProgress && _pausedAt != default)
            {
                // Resuming after Stop. The monitoring dashboard and the element life
                // counters already continue from where they were, so the machine timer
                // must do the same. Shifting both reference times forward by however
                // long the machine sat stopped discounts the paused span, which keeps
                // the timer, the production chart and the recorded process duration in
                // agreement and all measuring running time only.
                var pausedFor = DateTime.Now - _pausedAt;
                _machineStartsAt += pausedFor;
                _processStartTime += pausedFor;
            }
            else
            {
                // A fresh production run: everything starts from zero.
                _machineStartsAt = DateTime.Now;
                _processStartTime = DateTime.Now;
                _timerMs = 0;
                _torquePoints.Clear();
                _productionPoints.Clear();
                _t = 0;
                _runInProgress = true;
            }

            _pausedAt = default;
            _timer.Start();
            Monitoring.Start();   // resumes from a paused state, or starts fresh after a reset
            CanStart = false; CanStop = true; CanEndProcess = true;

            // RUN: load the monitored elements and their persistent ConsumedLife once,
            // then start accumulating life against the machine's live shaft speed.
            BeginLifeMonitoring();
        }

        private void StopMachine()
        {
            _timer.Stop();
            _pausedAt = DateTime.Now;   // remember when, so Start can resume from here
            Monitoring.Pause();   // freeze everything, keep all values
            CanStart = true; CanStop = false;

            // PAUSE semantics: freeze the life counters but do NOT finalize them.
            PauseLifeMonitoring();
        }

        private void EndProcess()
        {
            _timer.Stop();
            try
            {
                var firstRow = ForceRows[0];
                Binder.Bind(
                    Convert.ToDouble(ExitAngleDisplay), firstRow.CuttingForce, firstRow.ActiveForce,
                    firstRow.FrictionForce, firstRow.ThrustForce, firstRow.ShearForce,
                    firstRow.NormalShear, firstRow.NormalRake, firstRow.CuttingForceMoment,
                    Convert.ToDouble(FrictionAngleDisplay), Convert.ToDouble(ShearAngleDisplay),
                    Convert.ToDouble(FrictionCorrCoeffDisplay),
                    Convert.ToDouble(EnterAngleDisplay), Convert.ToDouble(ExitAngleDisplay),
                    Convert.ToDouble(CenterCuttingAngleDisplay),
                    Convert.ToDouble(MaxCuttingVelocityDisplay), Convert.ToDouble(FeedVelocityDisplay),
                    Convert.ToDouble(NumberOfRotationsDisplay),
                    30, 200, TimeSpan.FromMilliseconds(_timerMs).TotalHours,
                    _SelectedWood!.Id, _machineStartsAt, DateTime.Now);
            }
            catch { }

            // The run is finished, so the next Start begins a fresh one rather than
            // resuming this one.
            _runInProgress = false;
            _pausedAt = default;

            Monitoring.Reset();   // clear charts, timers, totals - back to initial state
            CanEndProcess = false; CanStart = true; CanStop = false;

            // The cutting operation is complete: this is the point where the runtime
            // life accumulation becomes part of the persistent ConsumedLife.
            CompleteLifeMonitoringOperation();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timerMs += 300;
            TimeCounter = $"{TimeSpan.FromMilliseconds(_timerMs):hh\\:mm\\:ss}";

            double baseTorque = 50, oscillation = 10 * Math.Sin(_t), noise = _rand.NextDouble() * 2 - 1;
            _torquePoints.Add(new ObservablePoint(_t, baseTorque + oscillation + noise));
            _t += 0.05;
            if (_torquePoints.Count > 100) _torquePoints.RemoveAt(0);

            double elapsed = (DateTime.Now - _processStartTime).TotalSeconds;
            _productionPoints.Add(new ObservablePoint(elapsed, (11.0 / 3600.0) * elapsed));

            // Advance element life by real elapsed time. Guarded so a fault in the
            // maintenance system can never disturb the existing simulation.
            try { _lifeMonitor.Tick(); } catch { }
        }
        #endregion

        #region Chart Init
        private void InitCuttingForceChart()
        {
            CuttingForceSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = new ObservableCollection<ObservablePoint>(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#17463E"), 2),
                    Fill = new SolidColorPaint(SKColor.Parse("#17463E").WithAlpha(25)),
                    GeometrySize = 3, GeometryStroke = new SolidColorPaint(SKColor.Parse("#17463E"), 2),
                    LineSmoothness = 0
                }
            };
        }

        private void InitMomentChart()
        {
            MomentSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = new ObservableCollection<ObservablePoint>(),
                    Stroke = new SolidColorPaint(SKColor.Parse("#d30000"), 2),
                    Fill = new SolidColorPaint(SKColor.Parse("#d30000").WithAlpha(20)),
                    GeometrySize = 3, GeometryStroke = new SolidColorPaint(SKColor.Parse("#d30000"), 2),
                    LineSmoothness = 0
                }
            };
        }

        private void InitTorqueChart()
        {
            TorqueSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _torquePoints,
                    Stroke = new SolidColorPaint(SKColor.Parse("#E05C1A"), 3),
                    Fill = new SolidColorPaint(SKColor.Parse("#E05C1A").WithAlpha(40)),
                    GeometrySize = 0, LineSmoothness = 0.3
                }
            };
        }

        private void InitProductionChart()
        {
            ProductionSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = _productionPoints,
                    Stroke = new SolidColorPaint(SKColor.Parse("#17463E"), 2),
                    Fill = new SolidColorPaint(SKColor.Parse("#17463E").WithAlpha(30)),
                    GeometrySize = 0, LineSmoothness = 0
                }
            };
        }

        private void UpdateCuttingForceChart(double slope, double intercept, double xMax)
        {
            var points = (ObservableCollection<ObservablePoint>)CuttingForceSeries[0].Values!;
            points.Clear();
            for (double x = 0; x <= xMax; x += 0.000001)
                points.Add(new ObservablePoint(x * 1e6, slope * x + intercept));
        }

        private void UpdateMomentChart(double slope, double intercept, double xMax)
        {
            var points = (ObservableCollection<ObservablePoint>)MomentSeries[0].Values!;
            points.Clear();
            for (double x = 0; x <= xMax; x += 0.000001)
                points.Add(new ObservablePoint(x * 1e6, slope * x + intercept));
        }
        #endregion

        #region History
        private void LoadHistory()
        {
            try
            {
                HistoryList = Database.Utility.Utility.GetOperationsProcessHistory();
                HistoryRows.Clear();
                foreach (var p in HistoryList)
                {
                    HistoryRows.Add(new HistoryRow
                    {
                        ProcessNo = p.Id,
                        WoodType = $"{p.WoodType.Type} ({p.WoodType.Category})",
                        ProductDimension = $"{p.ProductionCondition.ProductWidth} x {p.ProductionCondition.ProductHeight}",
                        ProductionVolume = p.ProductionCondition.ProductionVolume,
                        TotalFees = p.ProductionCondition.TotalFees,
                        ConsumedElectricity = p.OperationCondition.ConsumedElectricity,
                        StartAt = p.auditTimestamp.StartAt.ToString("yyyy-MM-dd HH:mm"),
                        EndAt = p.auditTimestamp.EndAt.ToString("yyyy-MM-dd HH:mm")
                    });
                }

                // KPI cards
                TotalFeesCard = HistoryList.Sum(x => x.ProductionCondition.TotalFees).ToString("C");
                ConsumedEnergyCard = HistoryList.Sum(x => x.OperationCondition.ConsumedElectricity).ToString("F2") + " KWh";
                ProductionVolumeCard = HistoryList.Sum(x => x.ProductionCondition.ProductionVolume).ToString("F2") + " M³";
                TotalProcessesCard = HistoryList.Count.ToString();

                // Force analysis cards from CriticalValues
                if (HistoryList.Count > 0)
                {
                    AvgCuttingForceCard = HistoryList.Average(x => x.CriticalValues.CuttingForce).ToString("F2");
                    MaxCuttingForceCard = HistoryList.Max(x => x.CriticalValues.CuttingForce).ToString("F2");
                    AvgCuttingMomentCard = HistoryList.Average(x => x.CriticalValues.CuttingMoment).ToString("F2");
                    MaxCuttingMomentCard = HistoryList.Max(x => x.CriticalValues.CuttingMoment).ToString("F2");

                    double totalEnergy = HistoryList.Sum(x => x.OperationCondition.ConsumedElectricity);
                    double totalVolume = HistoryList.Sum(x => x.ProductionCondition.ProductionVolume);
                    EnergyEfficiencyCard = totalEnergy > 0 ? (totalVolume / totalEnergy).ToString("F3") : "N/A";

                    double totalFees = HistoryList.Sum(x => x.ProductionCondition.TotalFees);
                    AvgCostPerM3Card = totalVolume > 0 ? (totalFees / totalVolume).ToString("C") : "N/A";

                    HardwoodCount = HistoryList.Count(x => x.WoodType.Category == DataAccess.Enums.enWoodCategory.Hardwood).ToString();
                    SoftwoodCount = HistoryList.Count(x => x.WoodType.Category == DataAccess.Enums.enWoodCategory.Softwood).ToString();

                    // Period captions: the actual date span covered by the records.
                    var firstStart = HistoryList.Min(x => x.auditTimestamp.StartAt);
                    var lastEnd = HistoryList.Max(x => x.auditTimestamp.EndAt);
                    PeriodRangeText = firstStart.Date == lastEnd.Date
                        ? $"{firstStart:dd MMM yyyy}"
                        : $"{firstStart:dd MMM yyyy}  →  {lastEnd:dd MMM yyyy}";
                    AllProcessesScopeText = $"{HistoryList.Count} processes · {PeriodRangeText}";
                }
                else
                {
                    PeriodRangeText = "No data yet";
                    AllProcessesScopeText = "No data yet";
                }

                BuildHistoryCharts();
            }
            catch { }
        }

        private void BuildHistoryCharts()
        {
            // Production history trend
            var grouped = HistoryList
                .GroupBy(x => new DateTime(x.auditTimestamp.StartAt.Year, x.auditTimestamp.StartAt.Month, x.auditTimestamp.StartAt.Day, x.auditTimestamp.StartAt.Hour, 0, 0))
                .OrderBy(x => x.Key)
                .Select((g, i) => new { Index = (double)i, Production = g.Sum(y => y.ProductionCondition.ProductionVolume) })
                .ToList();

            var actualPoints = new ObservableCollection<ObservablePoint>(grouped.Select(g => new ObservablePoint(g.Index, g.Production)));

            // Moving average trend
            int window = 3;
            var trendPoints = new ObservableCollection<ObservablePoint>();
            for (int i = 0; i < grouped.Count; i++)
            {
                int start = Math.Max(0, i - window + 1);
                double avg = 0; int c = 0;
                for (int j = start; j <= i; j++) { avg += grouped[j].Production; c++; }
                trendPoints.Add(new ObservablePoint(grouped[i].Index, avg / c));
            }

            HistoryProductionSeries = new ISeries[]
            {
                new LineSeries<ObservablePoint>
                {
                    Values = actualPoints,
                    Stroke = new SolidColorPaint(SKColors.DodgerBlue, 1.5f),
                    GeometrySize = 4, GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue, 2),
                    Fill = new SolidColorPaint(SKColors.DodgerBlue.WithAlpha(20)),
                    LineSmoothness = 0, Name = "Production"
                },
                new LineSeries<ObservablePoint>
                {
                    Values = trendPoints,
                    Stroke = new SolidColorPaint(SKColors.OrangeRed, 3),
                    GeometrySize = 0, LineSmoothness = 0.5, Name = "Trend"
                }
            };

            // Wood type pie chart
            var woodGroups = HistoryList
                .GroupBy(x => x.WoodType.Type)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(6)
                .ToList();

            var pieColors = new[] { "#E05C1A", "#17463E", "#C89B3C", "#4E6E81", "#10B981", "#EF4444" };

            WoodTypePieSeries = woodGroups.Select((w, i) => new PieSeries<double>
            {
                Values = new[] { (double)w.Count },
                Name = w.Name,
                Fill = new SolidColorPaint(SKColor.Parse(pieColors[i % pieColors.Length])),
                Stroke = new SolidColorPaint(SKColors.White, 2),
                Pushout = i == 0 ? 6 : 0
            } as ISeries).ToArray();

            BuildForceComparisonChart();
            BuildForceRadarChart();
        }

        // NEW component: grouped columns comparing the main force components
        // (from CriticalValues in the DB) across the most recent processes.
        private void BuildForceComparisonChart()
        {
            var recent = HistoryList
                .OrderByDescending(x => x.auditTimestamp.StartAt)
                .Take(8)
                .Reverse()
                .ToList();

            ForceComparisonXAxes = new[]
            {
                new Axis
                {
                    Labels = recent.Select(p => $"P{p.Id}").ToArray(),
                    LabelsRotation = 0,
                    TextSize = 11,
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#6B7280"))
                }
            };

            ISeries ColumnFor(string name, string hex, Func<OperationsProcess, double> sel) =>
                new ColumnSeries<double>
                {
                    Name = name,
                    Values = recent.Select(sel).ToArray(),
                    Fill = new SolidColorPaint(SKColor.Parse(hex)),
                    Stroke = null,
                    MaxBarWidth = 18,
                    Rx = 3, Ry = 3
                };

            ForceComparisonSeries = new[]
            {
                ColumnFor("Cutting",  "#E05C1A", p => p.CriticalValues.CuttingForce),
                ColumnFor("Active",   "#17463E", p => p.CriticalValues.ActiveForce),
                ColumnFor("Thrust",   "#C89B3C", p => p.CriticalValues.ThrustForce),
                ColumnFor("Shear",    "#4E6E81", p => p.CriticalValues.ShearForce),
                ColumnFor("Friction", "#10B981", p => p.CriticalValues.FrictionForceOnRake),
            };
        }

        // NEW component: radar (polar) chart showing the full force profile of
        // the latest process so the operator can spot an unbalanced cut at a glance.
        private void BuildForceRadarChart()
        {
            var latest = HistoryList.OrderByDescending(x => x.auditTimestamp.StartAt).FirstOrDefault();
            if (latest == null) { ForceRadarSeries = Array.Empty<ISeries>(); return; }

            var cv = latest.CriticalValues;
            var values = new double[]
            {
                cv.CuttingForce, cv.ActiveForce, cv.ThrustForce, cv.ShearForce,
                cv.FrictionForceOnRake, cv.NormalForceToRake, cv.NormalForceToShear
            };

            ForceRadarAngleAxes = new[]
            {
                new PolarAxis
                {
                    Labels = new[] { "Cutting", "Active", "Thrust", "Shear", "Friction", "N.Rake", "N.Shear" },
                    LabelsPaint = new SolidColorPaint(SKColor.Parse("#17463E")),
                    TextSize = 11
                }
            };

            ForceRadarSeries = new ISeries[]
            {
                new PolarLineSeries<double>
                {
                    Values = values,
                    Name = $"Process #{latest.Id}",
                    Stroke = new SolidColorPaint(SKColor.Parse("#E05C1A"), 3),
                    Fill = new SolidColorPaint(SKColor.Parse("#E05C1A").WithAlpha(60)),
                    GeometrySize = 8,
                    GeometryFill = new SolidColorPaint(SKColor.Parse("#E05C1A")),
                    GeometryStroke = new SolidColorPaint(SKColors.White, 2),
                    LineSmoothness = 0.4
                }
            };
        }

        // Populates the animated inline detail panel (replaces the old MessageBox).
        private void ViewDetails(object? param)
        {
            if (param is not int processNo) return;
            var target = HistoryList.SingleOrDefault(x => x.Id == processNo);
            if (target == null) return;

            var cv = target.CriticalValues;
            var oc = target.OperationCondition;

            DetailProcessNo = processNo;
            DetailTitle = $"Process #{processNo}";
            DetailSubtitle = $"{target.WoodType.Type} ({target.WoodType.Category})  •  {target.auditTimestamp.StartAt:yyyy-MM-dd HH:mm}";

            DetailCuttingSpeed = $"{oc.CuttingSpeed:F2} m/min";
            DetailFeedRate = $"{oc.FeedRate:F2} m/min";
            DetailShaftSpeed = $"{oc.SheftSpeed:F0} RPM";
            DetailEnergy = $"{oc.ConsumedElectricity:F2} kWh";
            DetailFrictionAngle = $"{cv.FrictionAngle:F2}°";
            DetailShearAngle = $"{cv.ShearAngle:F2}°";
            DetailCenterAngle = $"{cv.CenterAngle:F2}°";
            DetailMoment = $"{cv.CuttingMoment:F2} N.m";

            // Build proportional force bars (relative to the largest force component).
            var forces = new (string Name, double Value, string Color)[]
            {
                ("Cutting Force",        cv.CuttingForce,        "#E05C1A"),
                ("Active Force",         cv.ActiveForce,         "#17463E"),
                ("Thrust Force",         cv.ThrustForce,         "#C89B3C"),
                ("Shear Force",          cv.ShearForce,          "#4E6E81"),
                ("Friction on Rake",     cv.FrictionForceOnRake, "#10B981"),
                ("Normal to Rake",       cv.NormalForceToRake,   "#1F5F5B"),
                ("Normal to Shear",      cv.NormalForceToShear,  "#EF4444"),
            };
            double max = forces.Max(f => Math.Abs(f.Value));
            if (max <= 0) max = 1;

            SelectedForceBars.Clear();
            foreach (var f in forces)
                SelectedForceBars.Add(new ForceBar
                {
                    Name = f.Name,
                    Value = $"{f.Value:F2} N",
                    Percent = Math.Abs(f.Value) / max * 100.0,
                    BarColor = f.Color
                });

            IsDetailVisible = true;
        }

        private void ExportPdf(object? param)
        {
            if (param is int processNo)
            {
                var process = HistoryList.SingleOrDefault(x => x.Id == processNo);
                if (process == null) return;
                string folder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\Reports\\HistoryProcess";
                Directory.CreateDirectory(folder);
                string path = $"{folder}\\Process_{processNo}.pdf";
                try
                {
                    PdfReportGenerator.GenerateProcessReport(process, path);
                    System.Windows.MessageBox.Show($"Report generated at:\n{path}", "Done");
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error: {ex.Message}", "Oops");
                }
            }
        }
        #endregion

        #region Maintenance & Predictive Maintenance

        #region Maintenance Collections & Cards
        public ObservableCollection<ElementHealthRow> ElementHealthRows { get; }
        public ObservableCollection<MaintenanceHistoryRow> MaintenanceRows { get; }

        private string _monitoredElementsCard = "0"; public string MonitoredElementsCard { get => _monitoredElementsCard; set => SetProperty(ref _monitoredElementsCard, value); }
        private string _elementsWarningCard = "0"; public string ElementsWarningCard { get => _elementsWarningCard; set => SetProperty(ref _elementsWarningCard, value); }
        private string _elementsCriticalCard = "0"; public string ElementsCriticalCard { get => _elementsCriticalCard; set => SetProperty(ref _elementsCriticalCard, value); }
        private string _elementsFailedCard = "0"; public string ElementsFailedCard { get => _elementsFailedCard; set => SetProperty(ref _elementsFailedCard, value); }
        private string _maintenanceOperationsCard = "0"; public string MaintenanceOperationsCard { get => _maintenanceOperationsCard; set => SetProperty(ref _maintenanceOperationsCard, value); }
        private string _maintenanceTotalCostCard = "$0.00"; public string MaintenanceTotalCostCard { get => _maintenanceTotalCostCard; set => SetProperty(ref _maintenanceTotalCostCard, value); }
        private string _healthiestElementCard = "N/A"; public string HealthiestElementCard { get => _healthiestElementCard; set => SetProperty(ref _healthiestElementCard, value); }
        private string _worstElementCard = "N/A"; public string WorstElementCard { get => _worstElementCard; set => SetProperty(ref _worstElementCard, value); }
        private string _worstElementDetail = ""; public string WorstElementDetail { get => _worstElementDetail; set => SetProperty(ref _worstElementDetail, value); }

        private string _warningThresholdText = "80%"; public string WarningThresholdText { get => _warningThresholdText; set => SetProperty(ref _warningThresholdText, value); }
        private string _maintenanceStatusMessage = ""; public string MaintenanceStatusMessage { get => _maintenanceStatusMessage; set => SetProperty(ref _maintenanceStatusMessage, value); }
        private string _emailConfigStatus = ""; public string EmailConfigStatus { get => _emailConfigStatus; set => SetProperty(ref _emailConfigStatus, value); }
        private bool _isEmailConfigured; public bool IsEmailConfigured { get => _isEmailConfigured; set => SetProperty(ref _isEmailConfigured, value); }
        #endregion

        #region Maintenance Commands
        public RelayCommand RecordMaintenanceCommand { get; }
        public RelayCommand SaveMaintenanceCommand { get; }
        public RelayCommand CancelMaintenanceCommand { get; }
        public RelayCommand RefreshMaintenanceCommand { get; }
        public RelayCommand DismissFailureAlertCommand { get; }
        public RelayCommand GoToMaintenanceFromAlertCommand { get; }
        #endregion

        #region Record-Maintenance Form
        private bool _isMaintenanceFormVisible; public bool IsMaintenanceFormVisible { get => _isMaintenanceFormVisible; set => SetProperty(ref _isMaintenanceFormVisible, value); }
        private int _maintenanceElementId;
        private string _maintenanceElementName = ""; public string MaintenanceElementName { get => _maintenanceElementName; set => SetProperty(ref _maintenanceElementName, value); }
        private string _maintenanceElementPosition = ""; public string MaintenanceElementPosition { get => _maintenanceElementPosition; set => SetProperty(ref _maintenanceElementPosition, value); }
        private string _maintenanceDoneBy = ""; public string MaintenanceDoneBy { get => _maintenanceDoneBy; set { SetProperty(ref _maintenanceDoneBy, value); OnPropertyChanged(nameof(CanSaveMaintenance)); CommandManager.InvalidateRequerySuggested(); } }
        private string _maintenanceCost = "0"; public string MaintenanceCost { get => _maintenanceCost; set => SetProperty(ref _maintenanceCost, value); }
        private string _maintenanceElementPrice = "0"; public string MaintenanceElementPrice { get => _maintenanceElementPrice; set => SetProperty(ref _maintenanceElementPrice, value); }
        private string _maintenanceStoppingTimeCost = "0"; public string MaintenanceStoppingTimeCost { get => _maintenanceStoppingTimeCost; set => SetProperty(ref _maintenanceStoppingTimeCost, value); }
        private bool _maintenanceResetLife = true; public bool MaintenanceResetLife { get => _maintenanceResetLife; set => SetProperty(ref _maintenanceResetLife, value); }
        private string _maintenanceFormError = ""; public string MaintenanceFormError { get => _maintenanceFormError; set => SetProperty(ref _maintenanceFormError, value); }

        public bool CanSaveMaintenance => !string.IsNullOrWhiteSpace(MaintenanceDoneBy);
        #endregion

        #region Failure Alert
        private bool _isFailureAlertVisible; public bool IsFailureAlertVisible { get => _isFailureAlertVisible; set => SetProperty(ref _isFailureAlertVisible, value); }
        private string _failureElementName = ""; public string FailureElementName { get => _failureElementName; set => SetProperty(ref _failureElementName, value); }
        private string _failureElementBadge = ""; public string FailureElementBadge { get => _failureElementBadge; set => SetProperty(ref _failureElementBadge, value); }
        private string _failurePosition = ""; public string FailurePosition { get => _failurePosition; set => SetProperty(ref _failurePosition, value); }
        private string _failureDefaultLife = ""; public string FailureDefaultLife { get => _failureDefaultLife; set => SetProperty(ref _failureDefaultLife, value); }
        private string _failureConsumedLife = ""; public string FailureConsumedLife { get => _failureConsumedLife; set => SetProperty(ref _failureConsumedLife, value); }
        private string _failureRemainingLife = ""; public string FailureRemainingLife { get => _failureRemainingLife; set => SetProperty(ref _failureRemainingLife, value); }
        private string _failureLifeUsed = ""; public string FailureLifeUsed { get => _failureLifeUsed; set => SetProperty(ref _failureLifeUsed, value); }
        private string _failureElementPrice = ""; public string FailureElementPrice { get => _failureElementPrice; set => SetProperty(ref _failureElementPrice, value); }
        private string _failureMachineRuntime = ""; public string FailureMachineRuntime { get => _failureMachineRuntime; set => SetProperty(ref _failureMachineRuntime, value); }
        private string _failureMachineSpeed = ""; public string FailureMachineSpeed { get => _failureMachineSpeed; set => SetProperty(ref _failureMachineSpeed, value); }
        private string _failureProduction = ""; public string FailureProduction { get => _failureProduction; set => SetProperty(ref _failureProduction, value); }
        private string _failureEnergy = ""; public string FailureEnergy { get => _failureEnergy; set => SetProperty(ref _failureEnergy, value); }
        private string _failureDetectedAt = ""; public string FailureDetectedAt { get => _failureDetectedAt; set => SetProperty(ref _failureDetectedAt, value); }
        private string _failureEmailStatus = ""; public string FailureEmailStatus { get => _failureEmailStatus; set => SetProperty(ref _failureEmailStatus, value); }
        private string _failureEmailColor = "#C89B3C"; public string FailureEmailColor { get => _failureEmailColor; set => SetProperty(ref _failureEmailColor, value); }
        private string _failureApproachingSummary = ""; public string FailureApproachingSummary { get => _failureApproachingSummary; set => SetProperty(ref _failureApproachingSummary, value); }
        #endregion

        // ------------------------------------------------------------------
        // Life-monitoring hooks called from the existing machine controls.
        // Each one is guarded so the maintenance system can never break RUN/STOP.
        // ------------------------------------------------------------------

        private void BeginLifeMonitoring()
        {
            try
            {
                _lifeMonitor.BeginRun(_NumberOfRotationsInRPM);
                RefreshEmailConfigurationStatus();
            }
            catch (Exception ex)
            {
                MaintenanceStatusMessage = "Could not start element life monitoring: " + ex.Message;
            }
        }

        private void PauseLifeMonitoring()
        {
            try { _lifeMonitor.Pause(); } catch { }
        }

        private void CompleteLifeMonitoringOperation()
        {
            try
            {
                bool persisted = _lifeMonitor.CompleteOperation();
                MaintenanceStatusMessage = persisted
                    ? "Element life saved for the completed operation."
                    : "Element life could not be saved for the completed operation.";

                if (CurrentPage == "Maintenance") LoadMaintenance();
            }
            catch (Exception ex)
            {
                MaintenanceStatusMessage = "Could not save element life: " + ex.Message;
            }
        }

        // ------------------------------------------------------------------
        // Failure handling
        // ------------------------------------------------------------------

        /// <summary>
        /// Raised once, on the UI thread, when an element reaches its rated life.
        /// Stops the machine through the application's existing stop path, makes the
        /// failure durable, then raises the alert and sends the e-mail.
        /// </summary>
        private void OnElementFailed(object? sender, ElementFailureEventArgs e)
        {
            if (_isHandlingFailure) return;
            _isHandlingFailure = true;

            try
            {
                var failed = e.FailedElement;
                double consumedAtFailure = failed.TotalConsumedLife;

                // 1. Stop the machine exactly as pressing the existing Stop button does.
                StopMachine();

                // 2. Make the failure and the life that caused it durable.
                _lifeMonitor.PersistAccumulatedLife();
                _lifeMonitor.PersistFailureFlag(failed.ElementId);

                // 3. Capture the machine's condition at the moment of failure.
                var snapshot = CaptureMachineState();

                // 4. Raise the on-screen alert immediately.
                ShowFailureAlert(failed, consumedAtFailure, snapshot);

                // 5. Gather history and send the e-mail without blocking the UI.
                _ = DispatchFailureEmailAsync(failed, consumedAtFailure, snapshot);

                if (CurrentPage == "Maintenance") LoadMaintenance();
            }
            catch (Exception ex)
            {
                MaintenanceStatusMessage = "Failure handling error: " + ex.Message;
            }
            finally
            {
                _isHandlingFailure = false;
            }
        }

        /// <summary>
        /// Machine condition at failure, built from the values the application already
        /// calculates - nothing here is recomputed independently.
        /// </summary>
        private MachineStateSnapshot CaptureMachineState()
        {
            double runtimeHours = TimeSpan.FromMilliseconds(_timerMs).TotalHours;

            // Production volume: the application's own volumetric production rate
            // (shown on the Home panel as "Volumetric rate [m3/hr]") over the runtime.
            double production = _VolumetricProductionRateMeter3Hour * runtimeHours;

            // Electricity: the same expression the End Process path uses in Binder.
            double energy = 0;
            try { energy = Utility.ElectrcityPricePerKiloWatt * runtimeHours; } catch { }

            return new MachineStateSnapshot
            {
                MachineStatus = "STOPPED - automatic maintenance stop",
                ProductionState = "Cutting operation interrupted by end-of-life detection",
                MachineRuntime = TimeCounter,
                MachineRuntimeInHours = runtimeHours,
                ProductionQuantityInCubicMeter = production,
                ConsumedElectricity = energy,
                MachineSpeedInRPM = _NumberOfRotationsInRPM,
                WoodType = _SelectedWood != null ? $"{_SelectedWood.Type} ({_SelectedWood.Category})" : "N/A",
                FailureDetectedAt = DateTime.Now
            };
        }

        private void ShowFailureAlert(MonitoredElement failed, double consumedAtFailure, MachineStateSnapshot state)
        {
            FailureElementName = failed.TypeName;
            FailureElementBadge = "#" + failed.OrderOfElementAtMachine;
            FailurePosition = failed.Description;
            FailureDefaultLife = $"{failed.DefaultLife:N0} {failed.UnitLabel}";
            FailureConsumedLife = $"{consumedAtFailure:N0} {failed.UnitLabel}";
            FailureRemainingLife = $"{Math.Max(0, failed.DefaultLife - consumedAtFailure):N0} {failed.UnitLabel}";
            FailureLifeUsed = failed.DefaultLife > 0
                ? (consumedAtFailure / failed.DefaultLife * 100.0).ToString("F2") + "%"
                : "N/A";
            FailureElementPrice = failed.Price.ToString("C");

            FailureMachineRuntime = state.MachineRuntime;
            FailureMachineSpeed = $"{state.MachineSpeedInRPM:F1} RPM";
            FailureProduction = $"{state.ProductionQuantityInCubicMeter:F4} m³";
            FailureEnergy = $"{state.ConsumedElectricity:F4} kWh";
            FailureDetectedAt = state.FailureDetectedAt.ToString("dddd dd MMM yyyy  HH:mm:ss");

            var approaching = _lifeMonitor.GetElementsApproachingFailure(failed.ElementId);
            FailureApproachingSummary = approaching.Count == 0
                ? $"No other element has passed the {MaintenanceSettings.WarningThresholdPercent:F0}% warning threshold."
                : $"{approaching.Count} other element(s) are above the {MaintenanceSettings.WarningThresholdPercent:F0}% warning threshold - worst: "
                  + $"{approaching[0].TypeName} #{approaching[0].OrderOfElementAtMachine} at {approaching[0].LifeUsedPercentage:F2}%.";

            FailureEmailStatus = "Preparing maintenance alert e-mail...";
            FailureEmailColor = "#C89B3C";
            IsFailureAlertVisible = true;
        }

        /// <summary>
        /// Builds the alert context from the database and sends the e-mail. Any failure
        /// here is reported on the alert but never undoes the detection or the stop.
        /// </summary>
        private async Task DispatchFailureEmailAsync(MonitoredElement failed,
            double consumedAtFailure, MachineStateSnapshot state)
        {
            try
            {
                int windowDays = MaintenanceSettings.MaintenanceHistoryWindowInDays;
                var approaching = _lifeMonitor.GetElementsApproachingFailure(failed.ElementId);

                var context = await Task.Run(() =>
                {
                    List<DataAccess.Entities.Maintenance> recent;
                    DataAccess.Entities.Maintenance? lastGeneral;

                    try { recent = MaintenanceCRUD.GetMaintenanceForElementSince(failed.ElementId, DateTime.Now.AddDays(-windowDays)); }
                    catch { recent = new List<DataAccess.Entities.Maintenance>(); }

                    try { lastGeneral = MaintenanceCRUD.GetLastMachineMaintenance(); }
                    catch { lastGeneral = null; }

                    return new MaintenanceAlertContext
                    {
                        FailedElement = failed,
                        ConsumedLifeAtFailure = consumedAtFailure,
                        MachineState = state,
                        ElementsApproachingFailure = approaching,
                        FailedElementRecentMaintenance = recent,
                        LastMachineMaintenance = lastGeneral,
                        MaintenanceHistoryWindowInDays = windowDays,
                        WarningThresholdPercent = MaintenanceSettings.WarningThresholdPercent
                    };
                }).ConfigureAwait(true);

                FailureEmailStatus = "Sending maintenance alert to " + MaintenanceSettings.Recipient + "...";

                var result = await MaintenanceEmailService.SendFailureAlertAsync(context).ConfigureAwait(true);

                if (result.Sent)
                {
                    FailureEmailColor = "#10B981";
                    FailureEmailStatus = result.Message
                        + (result.CatalogAttached ? " Catalogue attached." : " Catalogue unavailable.")
                        + (result.ElementImageAttached ? " Element picture attached." : " Element picture unavailable.");
                }
                else
                {
                    FailureEmailColor = "#EF4444";
                    FailureEmailStatus = result.Message
                        + (result.SavedCopyPath != null ? " A copy was saved to " + result.SavedCopyPath : "");
                }

                MaintenanceStatusMessage = FailureEmailStatus;
            }
            catch (Exception ex)
            {
                FailureEmailColor = "#EF4444";
                FailureEmailStatus = "Maintenance alert could not be generated: " + ex.Message;
                MaintenanceStatusMessage = FailureEmailStatus;
            }
        }

        // ------------------------------------------------------------------
        // Maintenance screen
        // ------------------------------------------------------------------

        private void LoadMaintenance()
        {
            RefreshEmailConfigurationStatus();
            WarningThresholdText = MaintenanceSettings.WarningThresholdPercent.ToString("F0") + "%";

            LoadElementHealth();
            LoadMaintenanceHistory();
        }

        /// <summary>
        /// Current health of every monitored element. While a production operation is
        /// in progress the live in-memory counters are used, so the screen shows the
        /// life being consumed right now; otherwise the persisted values are read.
        /// </summary>
        private void LoadElementHealth()
        {
            try
            {
                var lastMaintenanceByElement = new Dictionary<int, DateTime>();
                try
                {
                    foreach (var record in MaintenanceCRUD.GetMaintenanceHistory())
                    {
                        if (!lastMaintenanceByElement.TryGetValue(record.ElementId, out var existing) ||
                            record.MaintenanceDate > existing)
                        {
                            lastMaintenanceByElement[record.ElementId] = record.MaintenanceDate;
                        }
                    }
                }
                catch { }

                List<MonitoredElement> elements;
                if (_lifeMonitor.Elements.Count > 0)
                {
                    elements = _lifeMonitor.Elements.ToList();
                }
                else
                {
                    elements = MaintenanceCRUD.GetMonitoredElements()
                        .Select(e => new MonitoredElement(e))
                        .ToList();
                }

                ElementHealthRows.Clear();
                foreach (var element in elements.OrderBy(e => e.OrderOfElementAtMachine))
                {
                    var status = element.HealthStatus;
                    ElementHealthRows.Add(new ElementHealthRow
                    {
                        ElementId = element.ElementId,
                        Order = element.OrderOfElementAtMachine,
                        Element = $"{element.TypeName} #{element.OrderOfElementAtMachine}",
                        ElementType = element.TypeName,
                        Position = element.Description,
                        ConsumedLife = $"{element.TotalConsumedLife:N0}",
                        DefaultLife = $"{element.DefaultLife:N0}",
                        RemainingLife = $"{element.RemainingLife:N0}",
                        LifeUsed = $"{element.LifeUsedPercentage:F4}%",
                        LifeUsedPercent = Math.Min(100, element.LifeUsedPercentage),
                        Status = status.ToString(),
                        StatusColor = StatusColour(status),
                        Price = element.Price.ToString("C"),
                        LastMaintenance = lastMaintenanceByElement.TryGetValue(element.ElementId, out var when)
                            ? when.ToString("yyyy-MM-dd HH:mm")
                            : "Never"
                    });
                }

                MonitoredElementsCard = ElementHealthRows.Count.ToString();
                ElementsWarningCard = ElementHealthRows.Count(r => r.Status == "Warning").ToString();
                ElementsCriticalCard = ElementHealthRows.Count(r => r.Status == "Critical").ToString();
                ElementsFailedCard = ElementHealthRows.Count(r => r.Status == "Failed").ToString();

                if (ElementHealthRows.Count > 0)
                {
                    var worst = ElementHealthRows.OrderByDescending(r => r.LifeUsedPercent).First();
                    var best = ElementHealthRows.OrderBy(r => r.LifeUsedPercent).First();
                    WorstElementCard = worst.Element;
                    WorstElementDetail = $"{worst.LifeUsed} of expected life used";
                    HealthiestElementCard = best.Element;
                }
            }
            catch (Exception ex)
            {
                MaintenanceStatusMessage = "Could not load element health: " + ex.Message;
            }
        }

        private void LoadMaintenanceHistory()
        {
            try
            {
                var records = MaintenanceCRUD.GetMaintenanceHistory();

                MaintenanceRows.Clear();
                foreach (var record in records)
                {
                    var element = record.Element;
                    var info = element?.ElementInformation;

                    MaintenanceRows.Add(new MaintenanceHistoryRow
                    {
                        MaintenanceId = record.Id,
                        Order = element?.OrderOfElementAtMachine ?? 0,
                        Element = info != null ? $"{info.Name} #{element!.OrderOfElementAtMachine}" : "N/A",
                        ElementType = info?.Name ?? "N/A",
                        Position = element?.Description ?? "N/A",
                        MaintenanceDate = record.MaintenanceDate.ToString("yyyy-MM-dd HH:mm"),
                        DoneBy = record.DoneBy,
                        Cost = record.Cost.ToString("C"),
                        ElementPrice = record.ElementPrice.ToString("C"),
                        StoppingTimeCost = record.StoppingTimeCost.ToString("C"),
                        TotalCost = record.TotalCost.ToString("C")
                    });
                }

                MaintenanceOperationsCard = records.Count.ToString();
                MaintenanceTotalCostCard = records.Sum(r => r.TotalCost).ToString("C");
            }
            catch (Exception ex)
            {
                MaintenanceStatusMessage = "Could not load maintenance history: " + ex.Message;
            }
        }

        private static string StatusColour(DataAccess.Enums.enElementHealthStatus status) => status switch
        {
            DataAccess.Enums.enElementHealthStatus.Failed => "#EF4444",
            DataAccess.Enums.enElementHealthStatus.Critical => "#E05C1A",
            DataAccess.Enums.enElementHealthStatus.Warning => "#C89B3C",
            _ => "#10B981"
        };

        private void RefreshEmailConfigurationStatus()
        {
            IsEmailConfigured = MaintenanceSettings.AreCredentialsConfigured;
            EmailConfigStatus = MaintenanceSettings.AreCredentialsConfigured
                ? "Maintenance alerts will be sent to " + MaintenanceSettings.Recipient + "."
                : MaintenanceSettings.CredentialStatusMessage;
        }

        // ------------------------------------------------------------------
        // Recording a maintenance / replacement operation
        // ------------------------------------------------------------------

        private void OpenMaintenanceForm(object? parameter)
        {
            if (parameter is not int elementId) return;

            var row = ElementHealthRows.FirstOrDefault(r => r.ElementId == elementId);
            if (row == null) return;

            _maintenanceElementId = elementId;
            MaintenanceElementName = row.Element;
            MaintenanceElementPosition = row.Position;
            MaintenanceDoneBy = "";
            MaintenanceCost = "0";
            MaintenanceElementPrice = row.Price.Replace("$", "").Replace(",", "").Trim();
            MaintenanceStoppingTimeCost = "0";
            MaintenanceResetLife = true;
            MaintenanceFormError = "";
            IsMaintenanceFormVisible = true;
        }

        private void SaveMaintenance()
        {
            if (!CanSaveMaintenance)
            {
                MaintenanceFormError = "Please enter who performed the maintenance.";
                return;
            }

            if (!TryParseAmount(MaintenanceCost, out double cost) ||
                !TryParseAmount(MaintenanceElementPrice, out double elementPrice) ||
                !TryParseAmount(MaintenanceStoppingTimeCost, out double stoppingCost))
            {
                MaintenanceFormError = "Costs must be numeric values.";
                return;
            }

            try
            {
                MaintenanceCRUD.RecordMaintenance(_maintenanceElementId, MaintenanceDoneBy.Trim(),
                    cost, elementPrice, stoppingCost, DateTime.Now, MaintenanceResetLife);

                // The persisted counters changed underneath the running session, so drop
                // it: the next RUN reloads the corrected values from the database.
                _lifeMonitor.InvalidateSession();

                IsMaintenanceFormVisible = false;
                MaintenanceStatusMessage = MaintenanceResetLife
                    ? $"Maintenance recorded for {MaintenanceElementName}. Consumed life reset to 0."
                    : $"Maintenance recorded for {MaintenanceElementName}.";

                LoadMaintenance();
            }
            catch (Exception ex)
            {
                MaintenanceFormError = "Could not save the maintenance record: " + ex.Message;
            }
        }

        private static bool TryParseAmount(string? text, out double value)
        {
            if (string.IsNullOrWhiteSpace(text)) { value = 0; return true; }
            text = text.Replace("$", "").Replace(",", "").Trim();
            return double.TryParse(text, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, out value);
        }

        #endregion
    }
}
