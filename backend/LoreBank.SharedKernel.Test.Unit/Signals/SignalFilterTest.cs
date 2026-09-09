using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.Signals;

[TestFixture]
[TestOf(typeof(SignalFilter))]
public sealed class SignalFilterTest
{
    private readonly static Guid AccountId = Guid.NewGuid();

    private readonly static Signal Deposited = new(
        Discriminant: "bank.money-deposited",
        ResourceKind: "bank-account",
        ResourceId: AccountId,
        OccurredAt: DateTimeOffset.UnixEpoch
    );

    [Test]
    public void All_ShouldMatchAnySignal()
    {
        SignalFilter.All.IsAll.Should().BeTrue();
        SignalFilter.All.Matches(Deposited).Should().BeTrue();
    }

    [Test]
    public void Parse_ShouldBeAll_WhenNoResourceIsGiven()
    {
        SignalFilter.Parse([]).IsAll.Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldBeTrue_WhenTheSignalResourceIsListed()
    {
        var filter = SignalFilter.Parse([$"bank-account/{AccountId}", $"probe-thing/{Guid.NewGuid()}"]);

        filter.IsAll.Should().BeFalse();
        filter.Matches(Deposited).Should().BeTrue();
    }

    [Test]
    public void Matches_ShouldBeFalse_WhenTheSignalResourceIsNotListed()
    {
        var filter = SignalFilter.Of([
                SignalResource.Of(
                    kind: "bank-account",
                    id: Guid.NewGuid()
                ),
            ]
        );

        filter.Matches(Deposited).Should().BeFalse();
        filter.Matches(Deposited with { ResourceKind = "probe-thing" }).Should().BeFalse();
    }

    [Test]
    public void Parse_ShouldThrow_WhenAResourceIsMalformed()
    {
        var act = () => SignalFilter.Parse([$"bank-account/{AccountId}", "nope"]);

        act.Should().Throw<InvalidSignalResourceException>();
    }
}
