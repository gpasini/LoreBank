using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(Iban))]
public sealed class IbanTest
{
    [Test]
    public void Parse_ShouldNormalize_WhenValueContainsSpacesAndLowercase()
    {
        // Act

        var iban = Iban.Parse("fr76 3000 6000 0112 3456 7890 189");

        // Assert

        iban.Value.Should().Be("FR7630006000011234567890189");
    }

    [Test]
    public void Parse_ShouldSucceed_WhenValueIsValid()
    {
        var act = () => Iban.Parse("DE89370400440532013000");

        act.Should().NotThrow();
    }

    [TestCase("")]
    [TestCase("FR76")]
    [TestCase("7676300060000112345678901")]
    [TestCase("FRXX300060000112345678901")]
    public void Parse_ShouldThrow_WhenValueIsInvalid(string value)
    {
        var act = () => Iban.Parse(value);

        act.Should().Throw<InvalidIbanException>();
    }

    [Test]
    public void Equals_ShouldBeTrue_WhenNormalizedValuesMatch()
    {
        Iban.Parse("FR7630006000011234567890189").Should().Be(Iban.Parse("fr76 3000 6000 0112 3456 7890 189"));
    }

    // La réhydratation truste la base (ADR 0016) : ni normalisation ni
    // validation — la valeur stockée est reprise telle quelle, même si elle
    // ne passerait plus Parse.
    [Test]
    public void Hydrate_ShouldAcceptTheStoredValueAsIs()
    {
        Iban.Hydrate("pas un iban").Value.Should().Be("pas un iban");
    }
}
