namespace DataAccess.Entities
{
    public class OperationCondition
    {
        private OperationCondition() { }

        public OperationCondition(double cuttingSpeed, double feedRate, double sheftSpeed, double consumedElectricity)
        {
            CuttingSpeed = cuttingSpeed;
            FeedRate = feedRate;
            SheftSpeed = sheftSpeed;
            ConsumedElectricity = consumedElectricity;
        }

        public int Id { get; private set; }
        public double CuttingSpeed { get; private set; }
        public double FeedRate { get; private set; }
        public double SheftSpeed { get; private set; }
        public double ConsumedElectricity { get; private set; }
        public virtual OperationsProcess OperationsProcess { get; private set; } = null!;
    }
}
