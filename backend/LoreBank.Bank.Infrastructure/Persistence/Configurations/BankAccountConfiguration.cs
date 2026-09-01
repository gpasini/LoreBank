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
                convertFromProviderExpression: value => new BankAccountId(value)
            )
            .ValueGeneratedNever();

        builder
            .Property(account => account.Iban)
            .HasColumnName("iban")
            .HasMaxLength(34)
            .HasConversion(
                convertToProviderExpression: iban => iban.Value,
                convertFromProviderExpression: value => new Iban(value)
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

        builder
            .Property(account => account.IsClosed)
            .HasColumnName("is_closed");

        builder.Ignore(account => account.DomainEvents);
    }
}
