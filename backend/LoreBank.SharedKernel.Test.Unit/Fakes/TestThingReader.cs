using LoreBank.SharedKernel.Infrastructure.Readers;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestThingReader(TestModuleDbContext context) : ModuleReader(context)
{
    public Task<TestThingRow?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => Query<TestThingRow>().SingleOrDefaultAsync(
        predicate: row => row.Id == id,
        cancellationToken: cancellationToken
    );

    // Passe-plat vers Query, pour que ModuleReaderTest éprouve la garde
    // keyless sur des types choisis par le test.
    public IQueryable<TRow> Expose<TRow>() where TRow : class => Query<TRow>();
}
