using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(Iban))]
public sealed class IbanTest
{
    [Test]
    public void Constructor_ShouldNormalize_WhenValueContainsSpacesAndLowercase()
    {
        // Act

        var iban = new Iban("fr76 3000 6000 0112 3456 7890 189");

        // Assert

        iban.Value.Should().Be("FR7630006000011234567890189");
    }

    [Test]
    public void Constructor_ShouldSucceed_WhenValueIsValid()
    {
        var act = () => new Iban("DE89370400440532013000");

        act.Should().NotThrow();
    }

    [TestCase("")]
    [TestCase("FR76")]
    [TestCase("7676300060000112345678901")]
    [TestCase("FRXX300060000112345678901")]
    public void Constructor_ShouldThrow_WhenValueIsInvalid(string value)
    {
        var act = () => new Iban(value);

        act.Should().Throw<InvalidIbanException>();
    }

    [Test]
    public void Equals_ShouldBeTrue_WhenNormalizedValuesMatch()
    {
        new Iban("FR7630006000011234567890189").Should().Be(new Iban("fr76 3000 6000 0112 3456 7890 189"));
    }
}
