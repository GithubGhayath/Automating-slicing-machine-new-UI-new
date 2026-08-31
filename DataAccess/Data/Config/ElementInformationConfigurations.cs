using DataAccess.Entities;
using DataAccess.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class ElementInformationConfigurations : IEntityTypeConfiguration<ElementInformation>
    {
        public void Configure(EntityTypeBuilder<ElementInformation> builder)
        {
            builder.ToTable("ElementsInformation");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
            builder.Property(x => x.DefaultLife).IsRequired().HasColumnType("float");
            builder.Property(x => x.LifeUnit)
                .HasConversion(x => x.ToString(), x => (enElementLifeUnit)Enum.Parse(typeof(enElementLifeUnit), x))
                .IsRequired().HasMaxLength(50);
            builder.Property(x => x.Price).IsRequired().HasColumnType("float");
            builder.Property(x => x.Image).HasMaxLength(1000);
            builder.Property(x => x.Catalog).HasMaxLength(1000);
        }
    }
}
