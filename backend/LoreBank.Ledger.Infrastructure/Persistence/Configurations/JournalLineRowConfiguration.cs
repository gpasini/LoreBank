using LoreBank.Ledger.Infrastructure.Persistence.ReadRows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Ledger.Infrastructure.Persistence.Configurations;

// ToView : requêtable, hors migrations — la table appartient au modèle
// d'écriture (JournalEntryConfiguration), la row ne fait que la lire, dans le
// schéma par défaut du module. Le lien colonne → propriété est en chaînes que
// rien ne compile : le test de relecture de chaque reader reste le filet.
public sealed class JournalLineRowConfiguration : IEntityTypeConfiguration<JournalLineRow>
{
    public void Configure(EntityTypeBuilder<JournalLineRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("journal_lines");

        builder.Property(row => row.JournalEntryId).HasColumnName("journal_entry_id");
        builder.Property(row => row.AccountRef).HasColumnName("account_ref");
        builder.Property(row => row.Direction).HasColumnName("direction");
        builder.Property(row => row.Amount).HasColumnName("amount");
        builder.Property(row => row.Currency).HasColumnName("currency");
    }
}
