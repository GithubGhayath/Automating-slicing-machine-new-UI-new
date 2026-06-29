using DataAccess.Entities;
using DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class WoodTypeConfigurations : IEntityTypeConfiguration<WoodType>
    {
        public void Configure(EntityTypeBuilder<WoodType> builder)
        {
            builder.ToTable("WoodTypes");
            builder.HasKey(w => w.Id);
            builder.Property(w => w.Id).ValueGeneratedOnAdd();
            builder.Property(w => w.Category)
                .HasConversion(x => x.ToString(), x => (enWoodCategory)Enum.Parse(typeof(enWoodCategory), x))
                .IsRequired().HasMaxLength(50);
            builder.Property(w => w.Type).IsRequired().HasMaxLength(100);
            builder.Property(w => w.ShearYieldStressInMpa).IsRequired().HasColumnType("float");
            builder.Property(w => w.SpecificWorkToSurfaceSeparationJoulPerMeter2).IsRequired().HasColumnType("float");
            builder.Property(w => w.CoefficientOfFriction).IsRequired().HasColumnType("float");
            builder.HasIndex(w => new { w.Type, w.Category }).IsUnique();
        }
    }
}
