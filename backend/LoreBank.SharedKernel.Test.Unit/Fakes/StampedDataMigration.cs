using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Jamais instanciée : la découverte et la dérivation d'id travaillent sur le
// type seul.
[DataMigration("20260101000000")]
public sealed class StampedDataMigration(ModuleDbContext context) : DataMigration(context)
{
    public override Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
