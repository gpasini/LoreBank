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
        modelBuilder.Entity<TestThing>().HasKey(thing => thing.Id);

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
