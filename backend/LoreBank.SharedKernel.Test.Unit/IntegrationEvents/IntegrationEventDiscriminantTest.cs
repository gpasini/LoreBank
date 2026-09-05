using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(IntegrationEventDiscriminant))]
public sealed class IntegrationEventDiscriminantTest
{
    [Test]
    public void Of_ShouldReturnTheDeclaredDiscriminant()
    {
        IntegrationEventDiscriminant.Of(typeof(PingIntegrationEvent)).Should().Be("probe.ping");
    }

    [Test]
    public void Of_ShouldThrow_WhenTheAttributeIsMissing()
    {
        var act = () => IntegrationEventDiscriminant.Of(typeof(UnattributedIntegrationEvent));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*UnattributedIntegrationEvent*discriminant*");
    }

    [Test]
    public void ModuleOf_ShouldReturnTheFirstSegment()
    {
        IntegrationEventDiscriminant.ModuleOf("bank.money-deposited").Should().Be("bank");
    }

    [TestCase("sanspoint")]
    [TestCase(".fait-sans-module")]
    public void ModuleOf_ShouldThrow_WhenTheShapeIsNotModuleDotFact(string discriminant)
    {
        var act = () => IntegrationEventDiscriminant.ModuleOf(discriminant);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*<module>.<fait>*");
    }
}
