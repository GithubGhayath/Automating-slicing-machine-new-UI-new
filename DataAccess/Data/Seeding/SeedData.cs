using DataAccess.Entities;
using DataAccess.Enums;

namespace DataAccess.Data.Seeding
{
    public static class SeedData
    {
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
    }
}
