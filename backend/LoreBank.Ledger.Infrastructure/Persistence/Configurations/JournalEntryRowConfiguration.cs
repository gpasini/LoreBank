using LoreBank.Ledger.Infrastructure.Persistence.ReadRows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Ledger.Infrastructure.Persistence.Configurations;

// ToView : requêtable, hors migrations — la table appartient au modèle
// d'écriture (JournalEntryConfiguration), la row ne fait que la lire, dans le
// schéma par défaut du module. Le lien colonne → propriété est en chaînes que
// rien ne compile : le test de relecture de chaque reader reste le filet.
public sealed class JournalEntryRowConfiguration : IEntityTypeConfiguration<JournalEntryRow>
{
    public void Configure(EntityTypeBuilder<JournalEntryRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("journal_entries");

        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.RecordedAt).HasColumnName("recorded_at");
    }
}
