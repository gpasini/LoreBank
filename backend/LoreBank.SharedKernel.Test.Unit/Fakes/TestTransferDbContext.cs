using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestTransferDbContext(
    DbContextOptions<TestTransferDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher,
    schema: "test"
)
{
    public DbSet<TestTransfer> Transfers => Set<TestTransfer>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestTransfer>(builder => {
                builder.HasKey(transfer => transfer.Id);

                // Les propriétés get-only de Money ne sont pas découvertes par
                // convention : on les déclare, comme les configurations des
                // modules le font.
                builder.OwnsOne(
                    navigationExpression: transfer => transfer.Debit,
                    buildAction: money => {
                        money.Property(m => m.Amount);
                        money.Property(m => m.Currency);
                    }
                );
                builder.OwnsOne(
                    navigationExpression: transfer => transfer.Credit,
                    buildAction: money => {
                        money.Property(m => m.Amount);
                        money.Property(m => m.Currency);
                    }
                );
            }
        );
    }
}
