namespace DataAccess.Entities.ValueObjects
{
    public class AuditTimestamp
    {
        public DateTime StartAt { get; set; }
        public DateTime EndAt { get; set; }
        public AuditTimestamp(DateTime createAt, DateTime endAt)
        {
            StartAt = createAt;
            EndAt = endAt;
        }
        public AuditTimestamp() { }
    }
}
