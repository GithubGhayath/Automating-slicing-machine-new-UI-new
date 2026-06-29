namespace MR200.UI.Helpers
{
    public static class clsMainEquations
    {
        public static double FeedPerTeeth_Unit_MeterPerTeeth(double CuttingVelocityInMeterPerMin, double FeedVelocityInMeterPerMin, double BladeDiameterInMeter, int NumberOfTooth)
        {
            return (FeedVelocityInMeterPerMin * Math.PI * BladeDiameterInMeter) / (NumberOfTooth * CuttingVelocityInMeterPerMin);
        }

        public static double NumberOfRotations_Unit_RPM(double FeedVelocityInMeterPerMin, double FeedPerTeethInMiterPerTeeth, int NumberOfTooth)
        {
            return FeedVelocityInMeterPerMin / (FeedPerTeethInMiterPerTeeth * NumberOfTooth);
        }

        public static double FrictionAngle_Unit_Degrees(double CoefficientOfFriction)
        {
            return clsHelper.RadiansToDegrees(Math.Atan(CoefficientOfFriction));
        }

        public static double ShearAngle_Unit_Degrees(double FrictionAngleInRadians, double RakeAngleInRadians)
        {
            double valueInRadians = (Math.PI / 4.0) - 0.5 * (FrictionAngleInRadians - RakeAngleInRadians);
            return clsHelper.RadiansToDegrees(valueInRadians);
        }

        public static double FrictionCorrectionCoefficient_Unit_None(double FrictionAngleInRadians, double ShearAngleInRadians, double RakeAngleInRadians)
        {
            return 1.0 - (Math.Sin(FrictionAngleInRadians) * Math.Sin(ShearAngleInRadians)) / (Math.Cos(FrictionAngleInRadians - RakeAngleInRadians) * Math.Cos(ShearAngleInRadians - RakeAngleInRadians));
        }

        public static double ShearingStrainAlongShearPlane(double ShearAngleInRadians, double RakeAngleInRadians)
        {
            return Math.Cos(RakeAngleInRadians) / (Math.Cos(ShearAngleInRadians - RakeAngleInRadians) * Math.Sin(ShearAngleInRadians));
        }

        public static double EnterAngle_Unit_Degrees(double eInMeter, double aInMeter, double RoInMeter)
        {
            return clsHelper.RadiansToDegrees(Math.Acos((aInMeter + eInMeter) / RoInMeter));
        }

        public static double ExitAngle_Unit_Degrees(double aInMeter, double RoInMeter)
        {
            return clsHelper.RadiansToDegrees(Math.Acos(aInMeter / RoInMeter));
        }

        public static double CenterAngleOfCutting(double eInMeter, double aInMeter, double RoInMeter)
        {
            return (EnterAngle_Unit_Degrees(eInMeter, aInMeter, RoInMeter) + ExitAngle_Unit_Degrees(aInMeter, RoInMeter)) / 2;
        }

        public static double TheMeanChipThickness_Unit_Meter(double CenterAngleInDegrees, double FeedPerTeethInMeter)
        {
            return FeedPerTeethInMeter * Math.Sin(clsHelper.DegreesToRadians(CenterAngleInDegrees));
        }

        public static Dictionary<double, double> ChipThicknessAtStudiedAngles(List<double> AnglesInDegrees, double FeedPerTeethInMeter)
        {
            var result = new Dictionary<double, double>();
            foreach (double angle in AnglesInDegrees)
                result.Add(angle, FeedPerTeethInMeter * Math.Sin(clsHelper.DegreesToRadians(angle)));
            return result;
        }

        public static double CuttingForce_Unit_Newton(double ShearYieldStressInMegaPas, double KerfThicknessInMeter, double ShearStrainAlongTheShearPlane,
            double FrictionCorrectionCoefficient, double TheMeanChipThicknessInMeter, double SpecificWorkOfASurfaceSeparationInJoulPerSqarMeter)
        {
            return (ShearYieldStressInMegaPas * 1000000 * KerfThicknessInMeter * ShearStrainAlongTheShearPlane / FrictionCorrectionCoefficient) * TheMeanChipThicknessInMeter
                + (SpecificWorkOfASurfaceSeparationInJoulPerSqarMeter * KerfThicknessInMeter / FrictionCorrectionCoefficient);
        }

        private static double _CalculateTheta_Unit_Radians()
        {
            double FrictionAngleInRadians = FrictionAngle_Unit_Degrees(Convert.ToDouble(clsHelper.ReadFromConfiguration("CoefficientOfFriction")));
            double RakeAngleInRadians = Convert.ToDouble(clsHelper.ReadFromConfiguration("RakeAngle"));
            return clsHelper.DegreesToRadians(90 - RakeAngleInRadians - (90 - FrictionAngleInRadians));
        }

        public static double ActiveForce_Unit_Newton(double CuttingForceInNewton) => CuttingForceInNewton / Math.Cos(_CalculateTheta_Unit_Radians());
        public static double FrictionForceOnRake_Unit_Newton(double FrictionAngleInRadians, double CuttingForceInNewton) => CuttingForceInNewton * Math.Sin(FrictionAngleInRadians);
        public static double ThrustForce_Unit_Newton(double CuttingForceInNewton) => Math.Tan(_CalculateTheta_Unit_Radians()) * CuttingForceInNewton;
        public static double ShearForce_Unit_Newton(double ShearAngleInRadians, double CuttingForceInNewton) => CuttingForceInNewton * Math.Cos(_CalculateTheta_Unit_Radians() + ShearAngleInRadians);
        public static double NormalForceToShearPlane_Unit_Newton(double ShearAngleInRadians, double CuttingForceInNewton) => CuttingForceInNewton * Math.Sin(_CalculateTheta_Unit_Radians() + ShearAngleInRadians);
        public static double NormalForceToRake_Unit_Newton(double FrictionAngleInRadians, double CuttingForceInNewton) => CuttingForceInNewton * Math.Cos(FrictionAngleInRadians);
        public static double MomentOfCuttingForce_Unit_NewtonMeter(double CuttingForceInNewton, double RadiusOfCuttingDiskInMeter) => CuttingForceInNewton * RadiusOfCuttingDiskInMeter;
        public static double VolumetricProductionRateMeter3PerHour(double FeedVelocityInMeterPerSecond, double ProductSectionAreaInMeter2) => (FeedVelocityInMeterPerSecond * ProductSectionAreaInMeter2) * 60 * 60;

        public static List<double> GetStudiedAngles(double eInMeter, double aInMeter, double RoInMeter, int TotalNumberOfTeeth)
        {
            var Angles = new List<double>();
            double _EnterAngle = EnterAngle_Unit_Degrees(eInMeter, aInMeter, RoInMeter);
            double _ExitAngle = ExitAngle_Unit_Degrees(aInMeter, RoInMeter);
            double spacing = 360.0 / TotalNumberOfTeeth;
            int count = (int)Math.Ceiling((_ExitAngle - _EnterAngle) / spacing);

            for (int i = 0; i < count; i++)
                Angles.Add(_ExitAngle - (i * spacing));
            Angles.Add(_EnterAngle);

            return Angles.OrderByDescending(e => e).ToList();
        }
    }
}
