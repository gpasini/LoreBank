using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

namespace LoreBank.SharedKernel.Test.Unit.IntegrationEvents;

[TestFixture]
[TestOf(typeof(PurgeCadence))]
public sealed class PurgeCadenceTest
{
    private readonly static DateTimeOffset Start = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero
    );

    [Test]
    public void IsDue_ShouldBeTrue_OnTheFirstPass()
    {
        var cadence = new PurgeCadence(TimeSpan.FromHours(1));

        cadence.IsDue(Start).Should().BeTrue();
    }

    [Test]
    public void IsDue_ShouldBeFalse_BeforeTheIntervalHasElapsed()
    {
        // Arrange

        var cadence = new PurgeCadence(TimeSpan.FromHours(1));

        cadence.IsDue(Start);

        // Act & Assert

        cadence.IsDue(Start.AddMinutes(59)).Should().BeFalse();
    }

    [Test]
    public void IsDue_ShouldBeTrueAgain_OnceTheIntervalHasElapsed()
    {
        // Arrange

        var cadence = new PurgeCadence(TimeSpan.FromHours(1));

        cadence.IsDue(Start);

        // Act & Assert

        cadence.IsDue(Start.AddHours(1)).Should().BeTrue();
    }

    [Test]
    public void IsDue_ShouldCountFromTheLastDuePass()
    {
        // Arrange

        var cadence = new PurgeCadence(TimeSpan.FromHours(1));

        cadence.IsDue(Start);
        cadence.IsDue(Start.AddHours(1));

        // Act & Assert

        cadence.IsDue(Start.AddMinutes(90)).Should().BeFalse();
        cadence.IsDue(Start.AddHours(2)).Should().BeTrue();
    }
}
