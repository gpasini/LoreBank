using LoreBank.SharedKernel.Infrastructure.Readers;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestThingReader(TestModuleDbContext context) : ModuleReader(context)
{
    public Task<TestThingRow?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => QuerySingleOrDefaultAsync(
        sql: """SELECT "Id" FROM "Things" WHERE "Id" = @id""",
        parameters: new() { ["id"] = id },
        map: reader => new TestThingRow(reader.GetGuid(0)),
        cancellationToken: cancellationToken
    );

    public Task<TestThingRow?> FailAsync(CancellationToken cancellationToken) => QuerySingleOrDefaultAsync(
        sql: "SELECT boom FROM nowhere",
        parameters: [],
        map: _ => new TestThingRow(Guid.Empty),
        cancellationToken: cancellationToken
    );
}
