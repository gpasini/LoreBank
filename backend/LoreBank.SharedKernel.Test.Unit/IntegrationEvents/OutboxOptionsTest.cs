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

    // Les défauts de l'ADR 0021 : un cloneur qui les change le fait en
    // configuration, pas en touchant au socle.
    [Test]
    public void Defaults_ShouldMatchAdr0021()
    {
        var options = new OutboxOptions();

        options.ReservationDuration.Should().Be(TimeSpan.FromMinutes(5));
        options.Retention.Should().Be(TimeSpan.FromDays(7));
        options.PurgeInterval.Should().Be(TimeSpan.FromHours(1));
    }
}
