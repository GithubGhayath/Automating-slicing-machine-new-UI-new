namespace DataAccess.Entities
{
    public class ProductionCondition
    {
        private ProductionCondition() { }

        public ProductionCondition(int productWidth, int productHeight, double productionVolume, double totalFees)
        {
            ProductWidth = productWidth;
            ProductHeight = productHeight;
            ProductionVolume = productionVolume;
            TotalFees = totalFees;
        }

        public int Id { get; private set; }
        public int ProductWidth { get; private set; }
        public int ProductHeight { get; private set; }
        public double ProductionVolume { get; private set; }
        public double TotalFees { get; private set; }
        public virtual OperationsProcess OperationsProcess { get; private set; } = null!;
    }
}
