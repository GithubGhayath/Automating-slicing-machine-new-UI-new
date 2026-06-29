using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    internal class OperationConditionConfigurations : IEntityTypeConfiguration<OperationCondition>
    {
        public void Configure(EntityTypeBuilder<OperationCondition> builder)
        {
            builder.ToTable("OperationConditions");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.CuttingSpeed).IsRequired();
            builder.Property(x => x.FeedRate).IsRequired();
            builder.Property(x => x.SheftSpeed).IsRequired();
            builder.Property(x => x.ConsumedElectricity).IsRequired();
        }
    }
}
