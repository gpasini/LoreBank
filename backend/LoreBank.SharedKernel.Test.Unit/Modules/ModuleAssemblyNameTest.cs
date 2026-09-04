using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.SharedKernel.Test.Unit.Modules;

[TestFixture]
[TestOf(typeof(ModuleAssemblyName))]
public sealed class ModuleAssemblyNameTest
{
    [Test]
    public void Parse_ShouldReturnRootAndModule_WhenTheNameFollowsTheConvention()
    {
        var (root, module) = ModuleAssemblyName.Parse("LoreBank.Bank.Infrastructure");

        root.Should().Be("LoreBank");
        module.Should().Be("Bank");
    }

    [Test]
    public void Parse_ShouldThrow_WhenTheNameHasTooFewSegments()
    {
        var act = () => ModuleAssemblyName.Parse("LoreBank.Infrastructure");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*LoreBank.Infrastructure*<Racine>.<Module>.Infrastructure*");
    }

    [Test]
    public void Parse_ShouldThrow_WhenTheRootHasSeveralSegments()
    {
        // Quatre segments, pas trois : la racine composée ferait diverger la
        // dérivation des assemblies et celle du préfixe des codes d'erreur,
        // qui lit le 2e segment du namespace (voir ADR 0007).
        var act = () => ModuleAssemblyName.Parse("Acme.Fin.Bank.Infrastructure");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Acme.Fin.Bank.Infrastructure*<Racine>.<Module>.Infrastructure*");
    }

    [Test]
    public void Parse_ShouldThrow_WhenTheLastSegmentIsNotInfrastructure()
    {
        var act = () => ModuleAssemblyName.Parse("LoreBank.Bank.Api");

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*LoreBank.Bank.Api*Infrastructure*");
    }
}
