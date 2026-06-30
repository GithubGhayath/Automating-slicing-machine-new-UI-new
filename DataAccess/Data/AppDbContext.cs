using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<WoodType> WoodTypes { get; set; }
        public DbSet<ProductionCondition> ProductionConditions { get; set; }
        public DbSet<CriticalValues> CriticalValues { get; set; }
        public DbSet<OperationsProcess> OperationsProcesses { get; set; }
        public DbSet<OperationCondition> OperationConditions { get; set; }
        public DbSet<ConstantValue> ConstantValues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            optionsBuilder.UseSqlServer("Server=.;Database=WoodCuttingMachine_MR200;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True");
        }
    }
}
