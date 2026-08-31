using DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DataAccess.Data.Config
{
    public class ElementConfigurations : IEntityTypeConfiguration<Element>
    {
        public void Configure(EntityTypeBuilder<Element> builder)
        {
            builder.ToTable("Elements");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.Description).IsRequired().HasMaxLength(250);
            builder.Property(x => x.ConsumedLife).IsRequired().HasColumnType("float");
            builder.Property(x => x.ImageOfElementAtMachine).HasMaxLength(1000);
            builder.Property(x => x.OrderOfElementAtMachine).IsRequired();
            builder.Property(x => x.FailureDetected).IsRequired().HasDefaultValue(false);

            // Each physical element occupies exactly one position on the machine.
            builder.HasIndex(x => x.OrderOfElementAtMachine).IsUnique();

            // One element <-> one element-information record. The unique foreign key
            // on the dependent side is what makes this a true one-to-one.
            builder.HasOne(e => e.ElementInformation)
                .WithOne(i => i.Element)
                .HasForeignKey<Element>(e => e.ElementInformationId)
                .OnDelete(DeleteBehavior.NoAction);
        }
    }
}
