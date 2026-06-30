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
using MR200.UI.Database.Utility;
using MR200.UI.Database.Wood;
using MR200.UI.Helpers;
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
            _machineStartsAt = DateTime.Now;
            _processStartTime = DateTime.Now;
            _timerMs = 0;
            _torquePoints.Clear();
            _productionPoints.Clear();
            _t = 0;
            _timer.Start();
            Monitoring.Start();   // resumes from a paused state, or starts fresh after a reset
            CanStart = false; CanStop = true; CanEndProcess = true;
        }

        private void StopMachine()
        {
            _timer.Stop();
            Monitoring.Pause();   // freeze everything, keep all values
            CanStart = true; CanStop = false;
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
            Monitoring.Reset();   // clear charts, timers, totals – back to initial state
            CanEndProcess = false; CanStart = true; CanStop = false;
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
    }
}
