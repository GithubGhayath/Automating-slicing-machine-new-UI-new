using DataAccess.Data;
using DataAccess.Entities;

namespace MR200.UI.Database
{
    public static class Binder
    {
        public static void Bind(double stadiedTheta, double cuttingForce, double activeForce,
            double frictionForceOnRake, double thrustForce, double shearForce,
            double normalForceToShear, double normalForceToRake, double cuttingMoment,
            double frictionAngle, double shearAngle, double frictionCorrectionCofficient,
            double enterAngle, double leavingAngle, double centerAngle,
            double cuttingSpeed, double feedRate, double sheftSpeed,
            int productWidth, int productHeight, double ProductionVolumePerHour,
            int WoodTypeId, DateTime StartAt, DateTime EndAt)
        {
            var criticalValues = new CriticalValues(stadiedTheta, cuttingForce, activeForce,
                frictionForceOnRake, thrustForce, shearForce, normalForceToShear, normalForceToRake, cuttingMoment,
                frictionAngle, shearAngle, frictionCorrectionCofficient, enterAngle, leavingAngle, centerAngle);

            double NumberOfPowerHours = (EndAt - StartAt).TotalHours;
            double consumedElectricity = Utility.Utility.ElectrcityPricePerKiloWatt * NumberOfPowerHours;

            var operationCondition = new OperationCondition(cuttingSpeed, feedRate, sheftSpeed, consumedElectricity);

            double productionVolume = ProductionVolumePerHour * NumberOfPowerHours;
            double totalFees = productionVolume * Utility.Utility.FeesPerCubicMetter;

            var productionCondition = new ProductionCondition(productWidth, productHeight, productionVolume, totalFees);
            var operationsProcess = new OperationsProcess(WoodTypeId, productionCondition, operationCondition, criticalValues, StartAt, EndAt);

            using var context = new AppDbContext();
            context.OperationsProcesses.Add(operationsProcess);
            context.SaveChanges();
        }
    }
}
