using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Volontairement sans [DataMigration] : le cas d'échec de DataMigrations.IdOf.
public sealed class UnstampedDataMigration(ModuleDbContext context) : DataMigration(context)
{
    public override Task ExecuteAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
