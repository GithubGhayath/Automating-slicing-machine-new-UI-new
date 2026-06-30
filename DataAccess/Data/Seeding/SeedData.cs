using DataAccess.Entities;
using DataAccess.Enums;

namespace DataAccess.Data.Seeding
{
    public static class SeedData
    {
        // Wood materials (mechanical properties from the original project)
        public static List<WoodType> GetWoods()
        {
            return new List<WoodType>
            {
                new WoodType { Category = enWoodCategory.Hardwood, Type = "Native beech", ShearYieldStressInMpa = 52.054, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 1345.917, CoefficientOfFriction = 0.651 },
                new WoodType { Category = enWoodCategory.Hardwood, Type = "Bendywood", ShearYieldStressInMpa = 49.75, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 905.139, CoefficientOfFriction = 0.69 },
                new WoodType { Category = enWoodCategory.Hardwood, Type = "DMDHEU", ShearYieldStressInMpa = 22.411, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 1236.611, CoefficientOfFriction = 0.848 },
                new WoodType { Category = enWoodCategory.Hardwood, Type = "Lignamon 783", ShearYieldStressInMpa = 43.292, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 3265.556, CoefficientOfFriction = 0.69 },
                new WoodType { Category = enWoodCategory.Hardwood, Type = "Lignamon 1185", ShearYieldStressInMpa = 30.39, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 3736.389, CoefficientOfFriction = 0.79 },
                new WoodType { Category = enWoodCategory.Hardwood, Type = "Beech 8", ShearYieldStressInMpa = 57.155, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 1772.611, CoefficientOfFriction = 0.565 },
                new WoodType { Category = enWoodCategory.Hardwood, Type = "Beech 16", ShearYieldStressInMpa = 50.609, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 1656.028, CoefficientOfFriction = 0.661 },
                new WoodType { Category = enWoodCategory.Softwood, Type = "Spruce 8", ShearYieldStressInMpa = 41.532, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 1555.361, CoefficientOfFriction = 0.455 },
                new WoodType { Category = enWoodCategory.Softwood, Type = "Spruce 16", ShearYieldStressInMpa = 35.994, SpecificWorkToSurfaceSeparationJoulPerMeter2 = 1472.444, CoefficientOfFriction = 0.541 },
            };
        }

        // Constant pricing values used to compute electricity cost and production fees
        public static ConstantValue GetConstantValue()
        {
            // ElectricityPricePerKiloWatt [price/KWh], FeesPerCubicMetter [price/m³]
            return new ConstantValue(0.15, 1200.0);
        }

        // Historical operation processes. Each one cascade-inserts its
        // ProductionCondition, OperationCondition and CriticalValues through
        // the navigation properties. WoodTypeId references the seeded WoodTypes (1..9).
        public static List<OperationsProcess> GetOperationsProcesses()
        {
            var now = DateTime.Now;

            return new List<OperationsProcess>
            {
                new OperationsProcess(1,
                    new ProductionCondition(30, 200, 0.85, 1020.0),
                    new OperationCondition(64, 11, 3100, 8.40),
                    new CriticalValues(64.62, 53.49, 54.23, 26.31, 8.91, 34.57, 40.81, 46.57, 14.55, 29.46, 22.46, 0.565, 41.85, 64.62, 53.23),
                    now.AddDays(-9).AddHours(-2), now.AddDays(-9).AddHours(-1)),

                new OperationsProcess(2,
                    new ProductionCondition(30, 200, 1.10, 1320.0),
                    new OperationCondition(64, 11, 3150, 9.10),
                    new CriticalValues(60.10, 50.12, 52.10, 25.10, 8.40, 33.10, 39.50, 45.20, 13.90, 28.90, 21.90, 0.580, 40.10, 60.10, 50.05),
                    now.AddDays(-8).AddHours(-3), now.AddDays(-8).AddHours(-1)),

                new OperationsProcess(3,
                    new ProductionCondition(30, 200, 1.35, 1620.0),
                    new OperationCondition(64, 11, 3200, 10.20),
                    new CriticalValues(58.30, 48.90, 51.00, 24.00, 8.10, 32.00, 38.20, 44.10, 13.40, 28.10, 21.10, 0.590, 39.30, 58.30, 49.15),
                    now.AddDays(-7).AddHours(-2), now.AddDays(-7).AddHours(-1)),

                new OperationsProcess(4,
                    new ProductionCondition(30, 200, 1.60, 1920.0),
                    new OperationCondition(64, 11, 3250, 11.30),
                    new CriticalValues(66.80, 55.00, 56.20, 27.10, 9.20, 35.40, 41.20, 47.80, 15.10, 30.10, 23.10, 0.600, 42.80, 66.80, 54.40),
                    now.AddDays(-6).AddHours(-4), now.AddDays(-6).AddHours(-2)),

                new OperationsProcess(5,
                    new ProductionCondition(30, 200, 1.90, 2280.0),
                    new OperationCondition(64, 11, 3300, 12.10),
                    new CriticalValues(70.50, 58.20, 59.00, 28.50, 9.80, 36.80, 43.00, 49.10, 16.00, 31.20, 24.20, 0.610, 44.50, 70.50, 57.25),
                    now.AddDays(-5).AddHours(-3), now.AddDays(-5).AddHours(-1)),

                new OperationsProcess(6,
                    new ProductionCondition(30, 200, 2.10, 2520.0),
                    new OperationCondition(64, 11, 3350, 13.40),
                    new CriticalValues(72.10, 60.40, 61.30, 29.20, 10.10, 37.50, 44.20, 50.40, 16.50, 32.00, 25.00, 0.620, 45.10, 72.10, 58.05),
                    now.AddDays(-4).AddHours(-2), now.AddDays(-4).AddHours(-1)),

                new OperationsProcess(7,
                    new ProductionCondition(30, 200, 2.35, 2820.0),
                    new OperationCondition(64, 11, 3400, 14.20),
                    new CriticalValues(68.90, 56.80, 57.60, 27.90, 9.50, 35.90, 42.10, 48.50, 15.40, 30.80, 23.80, 0.630, 43.90, 68.90, 56.45),
                    now.AddDays(-3).AddHours(-3), now.AddDays(-3).AddHours(-1)),

                new OperationsProcess(8,
                    new ProductionCondition(30, 200, 2.60, 3120.0),
                    new OperationCondition(64, 11, 3450, 15.10),
                    new CriticalValues(65.30, 54.10, 55.20, 26.40, 9.10, 34.20, 40.60, 46.90, 14.80, 29.70, 22.70, 0.640, 42.30, 65.30, 54.65),
                    now.AddDays(-2).AddHours(-4), now.AddDays(-2).AddHours(-2)),

                new OperationsProcess(9,
                    new ProductionCondition(30, 200, 2.90, 3480.0),
                    new OperationCondition(64, 11, 3500, 16.30),
                    new CriticalValues(63.00, 52.00, 53.50, 25.80, 8.80, 33.00, 39.40, 45.60, 14.20, 29.10, 22.10, 0.650, 41.00, 63.00, 52.50),
                    now.AddDays(-1).AddHours(-2), now.AddDays(-1).AddHours(-1)),

                new OperationsProcess(1,
                    new ProductionCondition(30, 200, 3.20, 3840.0),
                    new OperationCondition(64, 11, 3550, 17.20),
                    new CriticalValues(61.50, 51.00, 52.20, 25.00, 8.60, 32.20, 38.90, 44.80, 13.80, 28.60, 21.60, 0.660, 40.50, 61.50, 51.75),
                    now.AddHours(-6), now.AddHours(-5)),

                new OperationsProcess(6,
                    new ProductionCondition(30, 200, 3.50, 4200.0),
                    new OperationCondition(64, 11, 3600, 18.40),
                    new CriticalValues(59.80, 49.80, 50.90, 24.50, 8.30, 31.50, 38.00, 43.90, 13.50, 28.00, 21.00, 0.670, 39.80, 59.80, 49.90),
                    now.AddHours(-4), now.AddHours(-3)),

                new OperationsProcess(8,
                    new ProductionCondition(30, 200, 3.80, 4560.0),
                    new OperationCondition(64, 11, 3650, 19.10),
                    new CriticalValues(57.90, 48.60, 49.70, 23.90, 8.00, 30.80, 37.20, 42.80, 13.10, 27.50, 20.50, 0.680, 38.90, 57.90, 48.95),
                    now.AddHours(-2), now.AddHours(-1)),
            };
        }
    }
}
