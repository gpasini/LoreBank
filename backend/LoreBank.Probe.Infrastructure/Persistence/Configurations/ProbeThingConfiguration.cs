using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Probe.Infrastructure.Persistence.Configurations;

public sealed class ProbeThingConfiguration : IEntityTypeConfiguration<ProbeThing>
{
    public void Configure(EntityTypeBuilder<ProbeThing> builder)
    {
        builder.ToTable("probe_things");

        builder.HasKey(thing => thing.Id);

        builder
            .Property(thing => thing.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder
            .Property(thing => thing.Label)
            .HasColumnName("label")
            .HasMaxLength(200);

        builder
            .Property(thing => thing.Kind)
            .HasColumnName("kind")
            .HasMaxLength(50);

        builder
            .Property(thing => thing.Active)
            .HasColumnName("active");
    }
}
