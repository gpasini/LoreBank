using LoreBank.SharedKernel.Api.OpenApi;
using LoreBank.SharedKernel.Api.Validation;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.FakeModule.Domain.Exceptions;

namespace LoreBank.SharedKernel.Test.Unit.OpenApi;

// Le scan qui alimente le schéma ErrorCode de la Description : concrètes
// seulement, les codes du SharedKernel toujours présents, le 400 de binding
// aussi, le tout trié et sans doublon — la forme stable que le fichier commité
// doit garder d'un build à l'autre.
[TestFixture]
[TestOf(typeof(ErrorCodes))]
public sealed class ErrorCodesTest
{
    private static readonly IReadOnlyList<string> Codes = ErrorCodes.DiscoverIn([typeof(ModuleFailureException).Assembly]);

    [Test]
    public void DiscoverIn_ShouldDeriveTheCodeOfEachConcreteException_WhenScanningAnAssembly()
    {
        Codes.Should().Contain(DomainException.CodeOf(typeof(ModuleFailureException)));
        Codes.Should().Contain(DomainException.CodeOf(typeof(MissingThingException)));
    }

    [Test]
    public void DiscoverIn_ShouldSkipAbstractExceptions_WhenScanningAnAssembly()
    {
        Codes.Should().NotContain(DomainException.CodeOf(typeof(DomainException)));
        Codes.Should().NotContain(DomainException.CodeOf(typeof(NotFoundException)));
    }

    [Test]
    public void DiscoverIn_ShouldAlwaysIncludeTheSharedKernelAndBindingCodes_WhenNoAssemblyIsGiven()
    {
        var codes = ErrorCodes.DiscoverIn([]);

        codes.Should().Contain("INVALID_IBAN");
        codes.Should().Contain(ValidationProblemFactory.Code);
    }

    [Test]
    public void DiscoverIn_ShouldBeSortedAndDistinct_WhenScanningAnAssemblyTwice()
    {
        var codes = ErrorCodes.DiscoverIn([
            typeof(ModuleFailureException).Assembly,
            typeof(ModuleFailureException).Assembly,
        ]);

        codes.Should().OnlyHaveUniqueItems();
        codes.Should().BeInAscendingOrder(StringComparer.Ordinal);
    }
}
