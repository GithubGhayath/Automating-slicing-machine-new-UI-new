using DataAccess.Enums;

namespace DataAccess.Entities
{
    public class WoodType
    {
        public required enWoodCategory Category { get; set; }
        public int Id { get; set; }
        public required string Type { get; set; }
        public required double ShearYieldStressInMpa { get; set; }
        public required double SpecificWorkToSurfaceSeparationJoulPerMeter2 { get; set; }
        public required double CoefficientOfFriction { get; set; }
        public List<OperationsProcess> OperationsProcesses { get; set; } = new List<OperationsProcess>();
    }
}
