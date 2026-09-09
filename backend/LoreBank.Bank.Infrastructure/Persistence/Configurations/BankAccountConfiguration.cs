using LoreBank.Bank.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LoreBank.Bank.Infrastructure.Persistence.Configurations;

public sealed class BankAccountConfiguration : IEntityTypeConfiguration<BankAccount>
{
    public void Configure(EntityTypeBuilder<BankAccount> builder)
    {
        builder.ToTable("bank_accounts");

        builder.HasKey(account => account.Id);

        builder
            .Property(account => account.Id)
            .HasColumnName("id")
            .HasConversion(
                convertToProviderExpression: id => id.Value,
                convertFromProviderExpression: value => BankAccountId.Hydrate(value)
            )
            .ValueGeneratedNever();

        builder
            .Property(account => account.Iban)
            .HasColumnName("iban")
            .HasMaxLength(34)
            .HasConversion(
                convertToProviderExpression: iban => iban.Value,
                convertFromProviderExpression: value => Iban.Hydrate(value)
            );

        builder.OwnsOne(
            navigationExpression: account => account.Balance,
            buildAction: balance =>
            {
                balance
                    .Property(money => money.Amount)
                    .HasColumnName("balance_amount");

                balance
                    .Property(money => money.Currency)
                    .HasColumnName("balance_currency")
                    .HasMaxLength(3);
            }
        );

        // L'Anonyme est null hors du Domain (ADR 0023) : la forme de l'absence
        // que les readers ont déjà, et qu'un identifiant réel ne peut pas
        // contrefaire. Mappé sur le backing field nullable de l'agrégat, pas
        // sur OpenedBy : un convertisseur EF n'est jamais appelé sur un NULL,
        // c'est la propriété qui retraduit null en Anonyme.
        builder
            .Property<Actor?>("_openedBy")
            .HasColumnName("opened_by")
            .IsRequired(false)
            .HasConversion(
                convertToProviderExpression: actor => actor!.Id,
                convertFromProviderExpression: value => Actor.Hydrate(value)
            );

        builder
            .Property(account => account.IsClosed)
            .HasColumnName("is_closed");
    }
}
