using DataAccess.Enums;

namespace DataAccess.Entities
{
    /// <summary>
    /// Catalogue data for one physical element: what type it is, how long the
    /// manufacturer says it lasts, what it costs, and where to find its picture
    /// and technical catalogue.
    /// </summary>
    public class ElementInformation
    {
        private ElementInformation() { }

        public ElementInformation(string name, double defaultLife, enElementLifeUnit lifeUnit,
            double price, string? image = null, string? catalog = null)
        {
            Name = name;
            DefaultLife = defaultLife;
            LifeUnit = lifeUnit;
            Price = price;
            Image = image;
            Catalog = catalog;
        }

        public int Id { get; private set; }

        /// <summary>Element type / name, e.g. "Bearing 16009" or "A46 V-Belt".</summary>
        public string Name { get; private set; } = null!;

        /// <summary>Expected lifetime, expressed in <see cref="LifeUnit"/>.</summary>
        public double DefaultLife { get; private set; }

        /// <summary>
        /// Unit <see cref="DefaultLife"/> is measured in. Bearings are rated in
        /// revolutions, V-belts in passes; the two are never interchangeable.
        /// </summary>
        public enElementLifeUnit LifeUnit { get; private set; }

        /// <summary>Element price.</summary>
        public double Price { get; private set; }

        /// <summary>
        /// General picture of the element. Holds a URL; the maintenance e-mail
        /// downloads it at send time and embeds it.
        /// </summary>
        public string? Image { get; private set; }

        /// <summary>
        /// Manufacturer catalogue. Holds a file name relative to the application's
        /// MaintenanceAssets folder; attached to the maintenance e-mail.
        /// </summary>
        public string? Catalog { get; private set; }

        /// <summary>The single physical element this record describes (one-to-one).</summary>
        public virtual Element Element { get; private set; } = null!;

        public void SetImage(string? image) => Image = image;
        public void SetCatalog(string? catalog) => Catalog = catalog;

        /// <summary>Overrides the rated life. Used by controlled end-of-life testing.</summary>
        public void SetDefaultLife(double defaultLife) => DefaultLife = defaultLife;
    }
}
