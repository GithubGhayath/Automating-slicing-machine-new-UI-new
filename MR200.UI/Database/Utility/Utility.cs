using DataAccess.Data;
using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace MR200.UI.Database.Utility
{
    public static class Utility
    {
        public static double ElectrcityPricePerKiloWatt
        {
            get
            {
                using var context = new AppDbContext();
                return context.ConstantValues.First().ElectrcityPricePerKiloWatt;
            }
        }

        public static double FeesPerCubicMetter
        {
            get
            {
                using var context = new AppDbContext();
                return context.ConstantValues.First().FeesPerCubicMetter;
            }
        }

        public static List<OperationsProcess> GetOperationsProcessHistory()
        {
            using var context = new AppDbContext();
            return context.OperationsProcesses
                .Include(op => op.WoodType)
                .Include(op => op.OperationCondition)
                .Include(op => op.ProductionCondition)
                .Include(op => op.CriticalValues)
                .ToList();
        }
    }
}
