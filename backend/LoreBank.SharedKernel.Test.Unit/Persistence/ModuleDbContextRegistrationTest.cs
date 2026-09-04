using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Unit.Persistence;

[TestFixture]
[TestOf(typeof(ModuleDbContextRegistration))]
public sealed class ModuleDbContextRegistrationTest
{
    [Test]
    public void AddModuleDbContext_ShouldThrow_WhenTheConnectionStringIsMissing()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var act = () => services.AddModuleDbContext<TestModuleDbContext>(
            configuration: configuration,
            connectionStringName: "TestDb"
        );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*TestDb*ConnectionStrings*");
    }

    [Test]
    public void AddModuleDbContext_ShouldThrow_WhenEnlistIsDisabled()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            key: "ConnectionStrings:TestDb",
            value: "Host=localhost;Database=test;Enlist=false"
        );

        var act = () => services.AddModuleDbContext<TestModuleDbContext>(
            configuration: configuration,
            connectionStringName: "TestDb"
        );

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Enlist=false*transactionnelles*");
    }

    [Test]
    public void AddModuleDbContext_ShouldRegisterNpgsqlOptions_WhenTheConnectionStringIsValid()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            key: "ConnectionStrings:TestDb",
            value: "Host=localhost;Database=test"
        );

        services.AddModuleDbContext<TestModuleDbContext>(
            configuration: configuration,
            connectionStringName: "TestDb"
        );

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<DbContextOptions<TestModuleDbContext>>();

        options.Extensions
            .Should()
            .Contain(
                extension => extension.GetType().Name == "NpgsqlOptionsExtension",
                because: "le provider est une décision du socle, pas une ligne à recopier par module"
            );
    }

    [Test]
    public void AddModuleDbContext_ShouldAcceptAnExplicitEnlist_WhenItIsTrue()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            key: "ConnectionStrings:TestDb",
            value: "Host=localhost;Database=test;Enlist=true"
        );

        var act = () => services.AddModuleDbContext<TestModuleDbContext>(
            configuration: configuration,
            connectionStringName: "TestDb"
        );

        act.Should().NotThrow();
    }

    private static IConfiguration BuildConfiguration(
        string key,
        string value
    ) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
        .Build();
}
