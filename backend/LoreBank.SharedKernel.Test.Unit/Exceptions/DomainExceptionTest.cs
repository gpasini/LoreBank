using System.Globalization;
using LoreBank.FakeModule.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.Exceptions;

[TestFixture]
[TestOf(typeof(DomainException))]
public sealed class DomainExceptionTest
{
    [Test]
    public void Code_ShouldOmitThePrefix_WhenExceptionBelongsToTheSharedKernel()
    {
        // Arrange

        var exception = new TransverseFailureException(
            label: "iban",
            amount: 0m
        );

        // Assert

        exception.Code.Should().Be("TRANSVERSE_FAILURE");
    }

    [Test]
    public void Code_ShouldPrefixWithTheModule_WhenExceptionBelongsToAModule()
    {
        var exception = new ModuleFailureException("iban");

        exception.Code.Should().Be("FAKE_MODULE.MODULE_FAILURE");
    }

    [Test]
    public void Code_ShouldPrefixWithTheModule_WhenExceptionIsANotFoundException()
    {
        var exception = new MissingThingException(Guid.Empty);

        exception.Code.Should().Be("FAKE_MODULE.MISSING_THING");
    }

    [Test]
    public void Code_ShouldSeparateTheAcronymFromTheFollowingWord_WhenNameContainsOne()
    {
        var exception = new IBANFailureException();

        exception.Code.Should().Be("IBAN_FAILURE");
    }

    [Test]
    public void Parameters_ShouldExposeTheValues_WhenExceptionCarriesParameters()
    {
        var exception = new TransverseFailureException(
            label: "iban",
            amount: 20.50m
        );

        exception.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, object> {
                ["label"] = "iban",
                ["amount"] = 20.50m,
            }
        );
    }

    [Test]
    public void Parameters_ShouldBeEmpty_WhenExceptionCarriesNoParameter()
    {
        var exception = new ParameterlessFailureException();

        exception.Parameters.Should().BeEmpty();
    }

    [Test]
    public void Message_ShouldListTheParametersAfterTheCode_WhenExceptionCarriesParameters()
    {
        var exception = new TransverseFailureException(
            label: "iban",
            amount: 20.50m
        );

        // L'ordre d'énumération d'un Dictionary n'est pas garanti par contrat :
        // on assert le contenu, pas la séquence.
        exception.Message.Should().StartWith("TRANSVERSE_FAILURE (");
        exception.Message.Should().Contain("label=iban");
        exception.Message.Should().Contain("amount=20.50");
        exception.Message.Should().EndWith(")");
    }

    [Test]
    public void Message_ShouldBeTheCodeAlone_WhenExceptionCarriesNoParameter()
    {
        var exception = new ParameterlessFailureException();

        exception.Message.Should().Be("PARAMETERLESS_FAILURE");
    }

    [Test]
    public void Message_ShouldFormatValuesInvariantly_WhenCurrentCultureIsNotInvariant()
    {
        // Arrange

        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

        try {
            var exception = new TransverseFailureException(
                label: "iban",
                amount: 20.50m
            );

            // Assert

            exception.Message.Should().Contain("amount=20.50");
        } finally {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }
}
