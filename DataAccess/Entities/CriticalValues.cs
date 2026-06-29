namespace DataAccess.Entities
{
    public class CriticalValues
    {
        private CriticalValues() { }

        public CriticalValues(
            double stadiedTheta, double cuttingForce, double activeForce,
            double frictionForceOnRake, double thrustForce, double shearForce,
            double normalForceToShear, double normalForceToRake, double cuttingMoment,
            double frictionAngle, double shearAngle, double frictionCorrectionCofficient,
            double enterAngle, double leavingAngle, double centerAngle)
        {
            StadiedTheta = stadiedTheta;
            CuttingForce = cuttingForce;
            ActiveForce = activeForce;
            FrictionForceOnRake = frictionForceOnRake;
            ThrustForce = thrustForce;
            ShearForce = shearForce;
            NormalForceToShear = normalForceToShear;
            NormalForceToRake = normalForceToRake;
            CuttingMoment = cuttingMoment;
            FrictionAngle = frictionAngle;
            ShearAngle = shearAngle;
            FrictionCorrectionCofficient = frictionCorrectionCofficient;
            EnterAngle = enterAngle;
            LeavingAngle = leavingAngle;
            CenterAngle = centerAngle;
        }

        public int Id { get; private set; }
        public double StadiedTheta { get; private set; }
        public double CuttingForce { get; private set; }
        public double ActiveForce { get; private set; }
        public double FrictionForceOnRake { get; private set; }
        public double ThrustForce { get; private set; }
        public double ShearForce { get; private set; }
        public double NormalForceToShear { get; private set; }
        public double NormalForceToRake { get; private set; }
        public double CuttingMoment { get; private set; }
        public double FrictionAngle { get; private set; }
        public double ShearAngle { get; private set; }
        public double FrictionCorrectionCofficient { get; private set; }
        public double EnterAngle { get; private set; }
        public double LeavingAngle { get; private set; }
        public double CenterAngle { get; private set; }
        public virtual OperationsProcess OperationsProcess { get; private set; } = null!;
    }
}
