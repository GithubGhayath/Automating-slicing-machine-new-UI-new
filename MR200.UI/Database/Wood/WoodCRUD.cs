using DataAccess.Data;
using DataAccess.Entities;

namespace MR200.UI.Database.Wood
{
    public static class WoodCRUD
    {
        public static List<WoodType> GetWoodList()
        {
            using var context = new AppDbContext();
            return context.WoodTypes.ToList();
        }

        public static WoodType GetWoodByName(string woodName)
        {
            using var context = new AppDbContext();
            return context.WoodTypes.SingleOrDefault(w => w.Type == woodName)!;
        }
    }
}
