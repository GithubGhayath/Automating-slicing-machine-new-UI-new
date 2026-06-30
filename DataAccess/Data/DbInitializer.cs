using DataAccess.Data.Seeding;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Data
{
    /// <summary>
    /// Applies pending migrations (creating the database if needed) and seeds
    /// every table with starter data the first time the app runs.
    /// </summary>
    public static class DbInitializer
    {
        public static void Initialize()
        {
            using var context = new AppDbContext();

            // Create the database / apply any pending migrations.
            context.Database.Migrate();

            // Seed reference data only when the tables are empty so we never
            // duplicate rows on subsequent launches.
            if (!context.WoodTypes.Any())
            {
                context.WoodTypes.AddRange(SeedData.GetWoods());
                context.SaveChanges();
            }

            if (!context.ConstantValues.Any())
            {
                context.ConstantValues.Add(SeedData.GetConstantValue());
                context.SaveChanges();
            }

            // Historical processes cascade-insert their ProductionCondition,
            // OperationCondition and CriticalValues through navigation properties.
            if (!context.OperationsProcesses.Any())
            {
                context.OperationsProcesses.AddRange(SeedData.GetOperationsProcesses());
                context.SaveChanges();
            }
        }
    }
}
