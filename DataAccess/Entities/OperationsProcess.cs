using DataAccess.Entities.ValueObjects;

namespace DataAccess.Entities
{
    public class OperationsProcess
    {
        private OperationsProcess() { }

        public OperationsProcess(
            int woodTypeId,
            ProductionCondition productionCondition,
            OperationCondition operationCondition,
            CriticalValues criticalValues,
            DateTime? StartAt = null, DateTime? EndAd = null)
        {
            WoodTypeId = woodTypeId;
            ProductionCondition = productionCondition ?? throw new ArgumentNullException(nameof(productionCondition));
            OperationCondition = operationCondition ?? throw new ArgumentNullException(nameof(operationCondition));
            CriticalValues = criticalValues ?? throw new ArgumentNullException(nameof(criticalValues));
            auditTimestamp = new AuditTimestamp { StartAt = StartAt ?? DateTime.Now, EndAt = EndAd ?? DateTime.Now };
        }

        public int Id { get; private set; }
        public AuditTimestamp auditTimestamp { get; private set; } = null!;
        public int WoodTypeId { get; private set; }
        public int OperationConditionId { get; private set; }
        public int ProductionConditionId { get; private set; }
        public int CriticalValuesId { get; private set; }
        public virtual ProductionCondition ProductionCondition { get; private set; } = null!;
        public virtual OperationCondition OperationCondition { get; private set; } = null!;
        public virtual CriticalValues CriticalValues { get; private set; } = null!;
        public virtual WoodType WoodType { get; private set; } = null!;
    }
}
