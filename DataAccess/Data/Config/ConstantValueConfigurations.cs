using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class ConstantValueConfigurations : IEntityTypeConfiguration<ConstantValue>
    {
        public void Configure(EntityTypeBuilder<ConstantValue> builder)
        {
            builder.ToTable("ConstantValues");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.ElectrcityPricePerKiloWatt).IsRequired();
            builder.Property(x => x.FeesPerCubicMetter).IsRequired();
        }
    }
}
