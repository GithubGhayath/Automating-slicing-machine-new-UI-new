using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    internal class ProductionConditionConfigurations : IEntityTypeConfiguration<ProductionCondition>
    {
        public void Configure(EntityTypeBuilder<ProductionCondition> builder)
        {
            builder.ToTable("ProductionConditions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.ProductWidth).IsRequired();
            builder.Property(x => x.ProductHeight).IsRequired();
            builder.Property(x => x.ProductionVolume).IsRequired();
            builder.Property(x => x.TotalFees).IsRequired();
        }
    }
}
