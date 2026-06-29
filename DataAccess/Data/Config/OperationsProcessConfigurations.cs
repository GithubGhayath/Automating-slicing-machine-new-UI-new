using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class OperationsProcessConfigurations : IEntityTypeConfiguration<OperationsProcess>
    {
        public void Configure(EntityTypeBuilder<OperationsProcess> builder)
        {
            builder.ToTable("OperationsProcesses");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();

            builder.OwnsOne(x => x.auditTimestamp, a =>
            {
                a.Property(p => p.StartAt).HasColumnName("StartAt").IsRequired();
                a.Property(p => p.EndAt).HasColumnName("EndAt").IsRequired();
            });

            builder.HasOne(op => op.WoodType).WithMany(wt => wt.OperationsProcesses).HasForeignKey(op => op.WoodTypeId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(op => op.CriticalValues).WithOne(cv => cv.OperationsProcess).HasForeignKey<OperationsProcess>(op => op.CriticalValuesId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(op => op.OperationCondition).WithOne(oc => oc.OperationsProcess).HasForeignKey<OperationsProcess>(op => op.OperationConditionId).OnDelete(DeleteBehavior.NoAction);
            builder.HasOne(op => op.ProductionCondition).WithOne(pc => pc.OperationsProcess).HasForeignKey<OperationsProcess>(op => op.ProductionConditionId).OnDelete(DeleteBehavior.NoAction);
        }
    }
}
