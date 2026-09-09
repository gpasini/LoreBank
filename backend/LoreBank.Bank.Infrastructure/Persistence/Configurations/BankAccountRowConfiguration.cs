using LoreBank.Bank.Infrastructure.Persistence.ReadRows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Bank.Infrastructure.Persistence.Configurations;

// ToView : requêtable, hors migrations — la table appartient au modèle
// d'écriture (BankAccountConfiguration), la row ne fait que la lire, dans le
// schéma par défaut du module. Le lien colonne → propriété est en chaînes que
// rien ne compile : le test de relecture de chaque reader reste le filet.
public sealed class BankAccountRowConfiguration : IEntityTypeConfiguration<BankAccountRow>
{
    public void Configure(EntityTypeBuilder<BankAccountRow> builder)
    {
        builder.HasNoKey();
        builder.ToView("bank_accounts");

        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.Iban).HasColumnName("iban");
        builder.Property(row => row.BalanceAmount).HasColumnName("balance_amount");
        builder.Property(row => row.BalanceCurrency).HasColumnName("balance_currency");
        builder.Property(row => row.IsClosed).HasColumnName("is_closed");
        builder.Property(row => row.OpenedBy).HasColumnName("opened_by");
        builder.Property(row => row.OpenedAt).HasColumnName("opened_at");
    }
}
