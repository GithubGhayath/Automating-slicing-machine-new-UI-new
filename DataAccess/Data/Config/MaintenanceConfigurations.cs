using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class MaintenanceConfigurations : IEntityTypeConfiguration<Maintenance>
    {
        public void Configure(EntityTypeBuilder<Maintenance> builder)
        {
            builder.ToTable("Maintenance");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.DoneBy).IsRequired().HasMaxLength(150);
            builder.Property(x => x.Cost).IsRequired().HasColumnType("float");
            builder.Property(x => x.ElementPrice).IsRequired().HasColumnType("float");
            builder.Property(x => x.StoppingTimeCost).IsRequired().HasColumnType("float");
            builder.Property(x => x.MaintenanceDate).IsRequired();
            builder.Ignore(x => x.TotalCost);

            // One element has many maintenance records; every record belongs to one element.
            builder.HasOne(m => m.Element)
                .WithMany(e => e.Maintenances)
                .HasForeignKey(m => m.ElementId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.HasIndex(x => x.MaintenanceDate);
        }
    }
}
