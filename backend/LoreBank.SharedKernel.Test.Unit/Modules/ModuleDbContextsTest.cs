using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Modules;

// La politique de la résolution nom → DbContext, épinglée une fois pour ses
// cinq call-sites (publisher, processor) : correspondance insensible à la
// casse — le discriminant est en minuscules, ModuleName en Pascal — et échec
// qui nomme le module absent, le contexte de l'appelant et HostModules.All.
[TestFixture]
[TestOf(typeof(ModuleDbContexts))]
public sealed class ModuleDbContextsTest
{
    private TestModuleDbContext _dbContext = null!;

    private FakeServiceProvider _services = null!;

    private FakeHostModule _module = null!;

    [SetUp]
    public void CreateFakes()
    {
        _dbContext = new TestModuleDbContext(
            options: new DbContextOptionsBuilder<TestModuleDbContext>().UseSqlite("DataSource=:memory:").Options,
            dispatcher: new RecordingDomainEventDispatcher()
        );

        _services = new FakeServiceProvider();
        _services.RegisterInstance(_dbContext);

        _module = new FakeHostModule(
            moduleName: "Bank",
            dbContextType: typeof(TestModuleDbContext)
        );
    }

    [TearDown]
    public async Task DisposeDbContext() => await _dbContext.DisposeAsync();

    [Test]
    public void Resolve_ShouldMatchCaseInsensitively_WhenTheNameComesFromADiscriminant()
    {
        // Act — « bank », la casse d'un discriminant, face au ModuleName « Bank ».

        var resolved = ModuleDbContexts.Resolve(
            modules: [_module],
            services: _services,
            moduleName: "bank",
            purpose: "test"
        );

        // Assert

        resolved.Should().BeSameAs(_dbContext);
    }

    [Test]
    public void Resolve_ShouldThrowNamingTheModuleAndTheList_WhenNoModuleMatches()
    {
        // Act

        var act = () => ModuleDbContexts.Resolve(
            modules: [_module],
            services: _services,
            moduleName: "nowhere",
            purpose: "le contexte de l'appelant"
        );

        // Assert — le message porte le nom absent, le contexte du call-site,
        // et pointe la liste à corriger.

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*nowhere*le contexte de l'appelant*HostModules.All*");
    }

    [Test]
    public void Resolve_ShouldResolveTheDbContextOfTheModule_WhenCalledByModule()
    {
        var resolved = ModuleDbContexts.Resolve(
            services: _services,
            module: _module
        );

        resolved.Should().BeSameAs(_dbContext);
    }
}
