namespace DataAccess.Entities
{
    /// <summary>
    /// One maintenance / replacement operation carried out on a physical element.
    /// Records are never deleted, so the history survives element replacement.
    /// </summary>
    public class Maintenance
    {
        private Maintenance() { }

        public Maintenance(string doneBy, double cost, double elementPrice,
            double stoppingTimeCost, DateTime? maintenanceDate = null)
        {
            DoneBy = doneBy;
            Cost = cost;
            ElementPrice = elementPrice;
            StoppingTimeCost = stoppingTimeCost;
            MaintenanceDate = maintenanceDate ?? DateTime.Now;
        }

        public int Id { get; private set; }

        /// <summary>Person or team who performed the maintenance.</summary>
        public string DoneBy { get; private set; } = null!;

        /// <summary>Maintenance / service cost.</summary>
        public double Cost { get; private set; }

        /// <summary>Price of the replaced element.</summary>
        public double ElementPrice { get; private set; }

        /// <summary>Cost caused by machine downtime.</summary>
        public double StoppingTimeCost { get; private set; }

        public DateTime MaintenanceDate { get; private set; }

        public int ElementId { get; private set; }

        public virtual Element Element { get; private set; } = null!;

        /// <summary>Total cost of this operation.</summary>
        public double TotalCost => Cost + ElementPrice + StoppingTimeCost;
    }
}
