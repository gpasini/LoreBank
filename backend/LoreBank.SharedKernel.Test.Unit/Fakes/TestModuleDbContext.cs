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
    }
}
