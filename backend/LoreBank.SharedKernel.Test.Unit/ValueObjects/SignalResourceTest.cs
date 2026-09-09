using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(SignalResource))]
public sealed class SignalResourceTest
{
    private static readonly Guid Id = Guid.Parse("0f7d2a4e-9c3b-4b1e-8a6d-2f1c3e4d5a6b");

    [TestCase("bank-account")]
    [TestCase("thing")]
    [TestCase("a1-b2")]
    public void Of_ShouldKeepKindAndId_WhenTheKindIsKebabCase(string kind)
    {
        var resource = SignalResource.Of(
            kind: kind,
            id: Id
        );

        resource.Kind.Should().Be(kind);
        resource.Id.Should().Be(Id);
    }

    [TestCase("")]
    [TestCase("BankAccount")]
    [TestCase("bank_account")]
    [TestCase("bank-")]
    [TestCase("-bank")]
    [TestCase("bank account")]
    [TestCase("bank/account")]
    public void Of_ShouldThrow_WhenTheKindIsNotKebabCase(string kind)
    {
        var act = () => SignalResource.Of(
            kind: kind,
            id: Id
        );

        act.Should().Throw<InvalidSignalResourceException>()
            .Which.Should().Match<InvalidSignalResourceException>(exception =>
                exception.Code == "INVALID_SIGNAL_RESOURCE"
                && exception.Parameters["resource"].Equals($"{kind}/{Id}")
            );
    }

    [Test]
    public void Parse_ShouldReadKindAndId_WhenTheValueIsKindSlashGuid()
    {
        var resource = SignalResource.Parse($"bank-account/{Id}");

        resource.Kind.Should().Be("bank-account");
        resource.Id.Should().Be(Id);
    }

    [TestCase("bank-account")]
    [TestCase("bank-account/")]
    [TestCase("bank-account/not-a-guid")]
    [TestCase("/0f7d2a4e-9c3b-4b1e-8a6d-2f1c3e4d5a6b")]
    [TestCase("BankAccount/0f7d2a4e-9c3b-4b1e-8a6d-2f1c3e4d5a6b")]
    [TestCase("")]
    public void Parse_ShouldThrow_WhenTheValueIsMalformed(string value)
    {
        var act = () => SignalResource.Parse(value);

        act.Should().Throw<InvalidSignalResourceException>()
            .Which.Parameters["resource"].Should().Be(value);
    }

    [Test]
    public void ToString_ShouldRoundTripThroughParse()
    {
        var resource = SignalResource.Of(
            kind: "bank-account",
            id: Id
        );

        SignalResource.Parse(resource.ToString()).Should().Be(resource);
    }

    [Test]
    public void Equals_ShouldCompareKindAndId()
    {
        SignalResource.Of(
            kind: "bank-account",
            id: Id
        ).Should().Be(SignalResource.Parse($"bank-account/{Id}"));

        SignalResource.Of(
            kind: "bank-account",
            id: Id
        ).Should().NotBe(SignalResource.Of(
                kind: "probe-thing",
                id: Id
            )
        );
    }
}
