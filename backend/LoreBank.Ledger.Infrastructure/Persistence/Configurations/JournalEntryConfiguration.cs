using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Ledger.Infrastructure.Persistence.Configurations;

public sealed class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries");

        builder.HasKey(entry => entry.Id);

        builder
            .Property(entry => entry.Id)
            .HasColumnName("id")
            .HasConversion(
                convertToProviderExpression: id => id.Value,
                convertFromProviderExpression: value => new JournalEntryId(value)
            )
            .ValueGeneratedNever();

        // Les jambes sont des VO possédés par l'écriture : une table dédiée,
        // une clé technique invisible du domaine, et les conversions des VO
        // mono-valeur — le domaine ne voit jamais ces colonnes.
        builder.OwnsMany(
            navigationExpression: entry => entry.Lines,
            buildAction: lines =>
            {
                lines.ToTable("journal_lines");

                lines.WithOwner().HasForeignKey("journal_entry_id");

                lines.Property<Guid>("id").ValueGeneratedOnAdd();
                lines.HasKey("id");

                lines
                    .Property(line => line.Account)
                    .HasColumnName("account_ref")
                    .HasMaxLength(42)
                    .HasConversion(
                        convertToProviderExpression: account => account.Value,
                        convertFromProviderExpression: value => new LedgerAccountRef(value)
                    );

                lines
                    .Property(line => line.Direction)
                    .HasColumnName("direction")
                    .HasMaxLength(6)
                    .HasConversion<string>();

                lines.OwnsOne(
                    navigationExpression: line => line.Amount,
                    buildAction: amount =>
                    {
                        amount
                            .Property(money => money.Amount)
                            .HasColumnName("amount");

                        amount
                            .Property(money => money.Currency)
                            .HasColumnName("currency")
                            .HasMaxLength(3);
                    }
                );
            }
        );

        builder.Navigation(entry => entry.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
