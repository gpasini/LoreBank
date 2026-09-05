using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

namespace LoreBank.SharedKernel.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(OutboxOptions))]
public sealed class OutboxOptionsTest
{
    [Test]
    public void BackoffDelaySecondsFor_ShouldDoubleAtEachAttempt()
    {
        var options = new OutboxOptions { BackoffSeconds = 2 };

        options.BackoffDelaySecondsFor(1).Should().Be(2);
        options.BackoffDelaySecondsFor(2).Should().Be(4);
        options.BackoffDelaySecondsFor(3).Should().Be(8);
    }
}
