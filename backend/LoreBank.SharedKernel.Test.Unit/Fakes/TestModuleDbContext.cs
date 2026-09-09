using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestModuleDbContext(
    DbContextOptions<TestModuleDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher,
    schema: "test"
)
{
    public DbSet<TestThing> Things => Set<TestThing>();

    protected override void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestThing>(thing =>
        {
            thing.HasKey(t => t.Id);

            // Les propriétés get-only de Money ne sont pas découvertes
            // par convention : on les déclare, comme les configurations
            // des modules le font.
            thing.OwnsOne(
                navigationExpression: t => t.Price,
                buildAction: money =>
                {
                    money.Property(m => m.Amount);
                    money.Property(m => m.Currency);
                }
            );
        }
        );

        // La row keyless du fake : la même table que TestThing, lue sans clé —
        // la forme qu'un module donne à ses rows de lecture (ToView : hors
        // migrations, la table appartient au modèle d'écriture).
        modelBuilder.Entity<TestThingRow>(row =>
        {
            row.HasNoKey();
            row.ToView("Things");
        });
    }
}
