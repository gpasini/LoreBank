using LoreBank.Probe.Infrastructure.Persistence.ReadRows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Probe.Infrastructure.Persistence.Configurations;

public sealed class ProbeThingRowConfiguration : IEntityTypeConfiguration<ProbeThingRow>
{
    public void Configure(EntityTypeBuilder<ProbeThingRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("probe_things");

        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.Label).HasColumnName("label");
        builder.Property(row => row.Kind).HasColumnName("kind");
        builder.Property(row => row.Active).HasColumnName("active");
    }
}
