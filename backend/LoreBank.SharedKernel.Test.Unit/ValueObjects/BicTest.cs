using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(Bic))]
public sealed class BicTest
{
    [TestCase("BNPAFRPP")]
    [TestCase("BNPAFRPPXXX")]
    [TestCase("bnpafrpp")]
    public void Constructor_ShouldSucceed_WhenValueIsValid(string value)
    {
        var act = () => new Bic(value);

        act.Should().NotThrow();
    }

    [TestCase("")]
    [TestCase("BNPAFRPPX")]
    [TestCase("1NPAFRPP")]
    [TestCase("BNPAFRPPXXXX")]
    public void Constructor_ShouldThrow_WhenValueIsInvalid(string value)
    {
        var act = () => new Bic(value);

        act.Should().Throw<InvalidBicException>();
    }
}
