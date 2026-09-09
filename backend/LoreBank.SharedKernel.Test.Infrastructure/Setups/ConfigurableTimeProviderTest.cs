using LoreBank.SharedKernel.Test.Infrastructure.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// L'horloge du harnais (ADR 0024) : c'est elle que l'hôte de test sert à toute
// Application qui demande TimeProvider, elle rend l'Instant posé ou le vrai
// temps, et ResetFakes de la factory de base l'efface entre deux tests.
[TestFixture]
[TestOf(typeof(ConfigurableTimeProvider))]
public sealed class ConfigurableTimeProviderTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private readonly static DateTimeOffset Instant = new(
        year: 2026,
        month: 9,
        day: 9,
        hour: 8,
        minute: 30,
        second: 0,
        offset: TimeSpan.Zero
    );

    [Test]
    public void GetUtcNow_ShouldFollowTheSystemClock_WhenNoInstantIsSet()
    {
        var before = TimeProvider.System.GetUtcNow();

        var now = Factory.TimeProvider.GetUtcNow();

        now.Should().BeOnOrAfter(before).And.BeOnOrBefore(TimeProvider.System.GetUtcNow());
    }

    [Test]
    public void GetUtcNow_ShouldReturnTheInstant_WhenOneIsSet()
    {
        Factory.TimeProvider.Instant = Instant;

        Factory.TimeProvider.GetUtcNow().Should().Be(Instant);
    }

    [Test]
    public void Host_ShouldServeTheFake_WhenAnApplicationAsksForTimeProvider()
    {
        Factory.TimeProvider.Instant = Instant;

        Factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().Should().Be(Instant);
    }

    [Test]
    public void ResetFakes_ShouldClearTheInstant()
    {
        Factory.TimeProvider.Instant = Instant;

        Factory.ResetFakes();

        Factory.TimeProvider.Instant.Should().BeNull();
    }
}
