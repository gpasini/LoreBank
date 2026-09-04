using LoreBank.FakeModule.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.Repositories;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class TestThingRepository(TestModuleDbContext context)
    : ModuleRepository<TestThing, Guid>(context)
{
    protected override NotFoundException NotFound(Guid id) => new MissingThingException(id);
}
