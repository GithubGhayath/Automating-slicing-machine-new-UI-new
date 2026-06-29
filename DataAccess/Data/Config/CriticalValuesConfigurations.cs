using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    internal class CriticalValuesConfigurations : IEntityTypeConfiguration<CriticalValues>
    {
        public void Configure(EntityTypeBuilder<CriticalValues> builder)
        {
            builder.ToTable("CriticalValues");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.StadiedTheta).IsRequired();
            builder.Property(x => x.CuttingForce).IsRequired();
            builder.Property(x => x.ActiveForce).IsRequired();
            builder.Property(x => x.FrictionForceOnRake).IsRequired();
            builder.Property(x => x.ThrustForce).IsRequired();
            builder.Property(x => x.ShearForce).IsRequired();
            builder.Property(x => x.NormalForceToShear).IsRequired();
            builder.Property(x => x.NormalForceToRake).IsRequired();
            builder.Property(x => x.CuttingMoment).IsRequired();
            builder.Property(x => x.FrictionAngle).IsRequired();
            builder.Property(x => x.ShearAngle).IsRequired();
            builder.Property(x => x.FrictionCorrectionCofficient).IsRequired();
            builder.Property(x => x.EnterAngle).IsRequired();
            builder.Property(x => x.LeavingAngle).IsRequired();
            builder.Property(x => x.CenterAngle).IsRequired();
        }
    }
}
