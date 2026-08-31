using DataAccess.Entities;
using DataAccess.Enums;

namespace DataAccess.Data.Seeding
{
    /// <summary>
    /// Starter data for the maintenance system: the 16 physical elements the
    /// machine monitors, each with its own one-to-one element-information record.
    /// </summary>
    public static class MaintenanceSeedData
    {
        // ---- Element types -------------------------------------------------
        // Rated lives are supplied by the machine's mechanical analysis and are
        // stored in raw units (revolutions for bearings, passes for the V-belt).
        private const double Life16009 = 4_492_100_000d;   // 4492.1 million revolutions
        private const double Life16008 = 2_628_100_000d;   // 2628.1 million revolutions
        private const double Life16007 = 2_197_000_000d;   // 2197 million revolutions
        private const double LifeA46Belt = 288_000_000d;   // 2.88 x 10^8 passes

        private const double Price16009 = 47d;
        private const double Price16008 = 36d;
        private const double Price16007 = 31d;
        private const double PriceA46Belt = 10d;

        public const string Name16009 = "Bearing 16009";
        public const string Name16008 = "Bearing 16008";
        public const string Name16007 = "Bearing 16007";
        public const string NameA46Belt = "A46 V-Belt";

        // Catalogue file names, relative to the application's MaintenanceAssets\Catalogs folder.
        private const string Catalog16009 = "SKF_16009_Deep_Groove_Ball_Bearing.pdf";
        private const string Catalog16008 = "SKF_16008_Deep_Groove_Ball_Bearing.pdf";
        private const string Catalog16007 = "SKF_16007_Deep_Groove_Ball_Bearing.pdf";
        private const string CatalogA46Belt = "SKF_PHG_A46_V_Belt.pdf";

        // Product pages carrying the general picture of each element type. The
        // maintenance e-mail resolves the actual image from these at send time.
        private const string Image16009 = "https://www.amazon.it/-/en/LEPREM/dp/B0DKBKTMQY";
        private const string Image16008 = "https://nl.rs-online.com/web/p/ball-bearings/2091838";
        private const string Image16007 = "https://www.nskbearingcatalogue.com/product/16007-bearing/";
        private const string ImageA46Belt = "https://dieselbelting.com/products/a46-classic-wrapped-v-belt-1-2-x-48in-outside-circumference";

        private static ElementInformation Info16009() =>
            new(Name16009, Life16009, enElementLifeUnit.Revolution, Price16009, Image16009, Catalog16009);

        private static ElementInformation Info16008() =>
            new(Name16008, Life16008, enElementLifeUnit.Revolution, Price16008, Image16008, Catalog16008);

        private static ElementInformation Info16007() =>
            new(Name16007, Life16007, enElementLifeUnit.Revolution, Price16007, Image16007, Catalog16007);

        private static ElementInformation InfoA46Belt() =>
            new(NameA46Belt, LifeA46Belt, enElementLifeUnit.Pass, PriceA46Belt, ImageA46Belt, CatalogA46Belt);

        /// <summary>
        /// The 16 monitored physical elements, in machine order.
        ///
        /// Orders 1-8   : the four feeding shafts, counted from the right. Each shaft is
        ///                carried by two 16009 bearings - one at the end of the shaft and
        ///                one immediately next to it.
        /// Orders 9-12  : the two cutting shafts. Each carries one 16007 and one 16008.
        /// Orders 13-16 : the A46 V-belt driving each feeding shaft from the motor.
        ///
        /// ImageOfElementAtMachine is set from the machine-position photographs shipped
        /// in MaintenanceAssets\Images, each named after the element's description.
        /// </summary>
        /// <summary>
        /// Extension of the machine-position pictures shipped in MaintenanceAssets\Images.
        /// Each file is named exactly after the element's Description.
        /// </summary>
        private const string PositionImageExtension = ".png";

        public static List<Element> GetElements()
        {
            var elements = new List<Element>
            {
                // ---- Feeding shafts (16009) ----
                new("Feeding shaft 1 (first from right) - bearing at the end of the shaft", 1, Info16009()),
                new("Feeding shaft 1 (first from right) - bearing next to the shaft-end bearing", 2, Info16009()),
                new("Feeding shaft 2 (second from right) - bearing at the end of the shaft", 3, Info16009()),
                new("Feeding shaft 2 (second from right) - bearing next to the shaft-end bearing", 4, Info16009()),
                new("Feeding shaft 3 (third from right) - bearing at the end of the shaft", 5, Info16009()),
                new("Feeding shaft 3 (third from right) - bearing next to the shaft-end bearing", 6, Info16009()),
                new("Feeding shaft 4 (fourth from right) - bearing at the end of the shaft", 7, Info16009()),
                new("Feeding shaft 4 (fourth from right) - bearing next to the shaft-end bearing", 8, Info16009()),

                // ---- Cutting shafts (16007 + 16008 each) ----
                new("Top cutting shaft - 16007 bearing", 9, Info16007()),
                new("Top cutting shaft - 16008 bearing", 10, Info16008()),
                new("Bottom cutting shaft - 16007 bearing", 11, Info16007()),
                new("Bottom cutting shaft - 16008 bearing", 12, Info16008()),

                // ---- Motor-to-feeding-shaft V-belts ----
                new("V-belt driving feeding shaft 1 (first from right)", 13, InfoA46Belt()),
                new("V-belt driving feeding shaft 2 (second from right)", 14, InfoA46Belt()),
                new("V-belt driving feeding shaft 3 (third from right)", 15, InfoA46Belt()),
                new("V-belt driving feeding shaft 4 (fourth from right)", 16, InfoA46Belt()),
            };

            // Machine-position picture: the file shipped in MaintenanceAssets\Images is
            // named exactly after the element's description, so the link is derived here
            // instead of being duplicated on all sixteen lines above. The maintenance
            // e-mail embeds this picture so the technician can find the part physically.
            foreach (var element in elements)
                element.SetImageOfElementAtMachine(element.Description + PositionImageExtension);

            return elements;
        }
    }
}
