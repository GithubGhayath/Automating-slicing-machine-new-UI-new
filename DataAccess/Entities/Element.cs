namespace DataAccess.Entities
{
    /// <summary>
    /// One physical element mounted in the machine. Elements of the same type stay
    /// separate rows so each keeps its own consumed life, maintenance history and
    /// machine position.
    /// </summary>
    public class Element
    {
        private Element() { }

        public Element(string description, int orderOfElementAtMachine,
            ElementInformation elementInformation, double consumedLife = 0,
            string? imageOfElementAtMachine = null)
        {
            Description = description;
            OrderOfElementAtMachine = orderOfElementAtMachine;
            ElementInformation = elementInformation ?? throw new ArgumentNullException(nameof(elementInformation));
            ConsumedLife = consumedLife;
            ImageOfElementAtMachine = imageOfElementAtMachine;
        }

        public int Id { get; private set; }

        /// <summary>Description of the physical element and where it sits.</summary>
        public string Description { get; private set; } = null!;

        /// <summary>
        /// Persistent cumulative consumed life, in the unit of the element's
        /// <see cref="ElementInformation.LifeUnit"/>. Never a session value: it is
        /// loaded on RUN and written back when a production operation completes.
        /// </summary>
        public double ConsumedLife { get; private set; }

        /// <summary>
        /// Picture showing where this element sits inside the machine. Left empty
        /// until the machine-position photographs are supplied.
        /// </summary>
        public string? ImageOfElementAtMachine { get; private set; }

        /// <summary>
        /// Numeric identifier used to find the element physically on the machine
        /// (1-16). See the maintenance documentation for the position map.
        /// </summary>
        public int OrderOfElementAtMachine { get; private set; }

        /// <summary>
        /// Latched once this element has reached its rated life, so the monitoring
        /// loop raises exactly one failure event per element instead of repeating
        /// the stop-and-notify every tick. Cleared when the element is replaced.
        /// </summary>
        public bool FailureDetected { get; private set; }

        public int ElementInformationId { get; private set; }

        public virtual ElementInformation ElementInformation { get; private set; } = null!;
        public virtual ICollection<Maintenance> Maintenances { get; private set; } = new List<Maintenance>();

        /// <summary>Adds runtime-accumulated life onto the persistent counter.</summary>
        public void AccumulateLife(double additionalLife)
        {
            if (double.IsNaN(additionalLife) || double.IsInfinity(additionalLife) || additionalLife <= 0) return;
            ConsumedLife += additionalLife;
        }

        public void MarkFailureDetected() => FailureDetected = true;

        public void SetImageOfElementAtMachine(string? image) => ImageOfElementAtMachine = image;

        /// <summary>
        /// Element was physically replaced: life restarts from zero and the element
        /// is eligible to raise a failure event again. Maintenance history is kept.
        /// </summary>
        public void ResetLifeAfterReplacement()
        {
            ConsumedLife = 0;
            FailureDetected = false;
        }
    }
}
