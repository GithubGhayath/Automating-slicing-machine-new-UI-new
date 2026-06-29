namespace DataAccess.Entities
{
    public class ConstantValue
    {
        private ConstantValue() { }

        public ConstantValue(double electrcityPricePerKiloWatt, double feesPerCubicMetter)
        {
            ElectrcityPricePerKiloWatt = electrcityPricePerKiloWatt;
            FeesPerCubicMetter = feesPerCubicMetter;
        }

        public int Id { get; private set; }
        public double ElectrcityPricePerKiloWatt { get; private set; }
        public double FeesPerCubicMetter { get; private set; }
    }
}
