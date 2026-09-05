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
    public void Parse_ShouldSucceed_WhenValueIsValid(string value)
    {
        var act = () => Bic.Parse(value);

        act.Should().NotThrow();
    }

    [TestCase("")]
    [TestCase("BNPAFRPPX")]
    [TestCase("1NPAFRPP")]
    [TestCase("BNPAFRPPXXXX")]
    public void Parse_ShouldThrow_WhenValueIsInvalid(string value)
    {
        var act = () => Bic.Parse(value);

        act.Should().Throw<InvalidBicException>();
    }

    [Test]
    public void Hydrate_ShouldAcceptTheStoredValueAsIs()
    {
        Bic.Hydrate("pas un bic").Value.Should().Be("pas un bic");
    }
}
