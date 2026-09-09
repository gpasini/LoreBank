using LoreBank.SharedKernel.Api.Signals;

namespace LoreBank.SharedKernel.Test.Unit.Signals;

[TestFixture]
[TestOf(typeof(SignalOptions))]
public sealed class SignalOptionsTest
{
    [Test]
    public void Defaults_ShouldKeepAConnectionAliveEveryFifteenSeconds()
    {
        var options = new SignalOptions();

        options.SectionNameShouldBeSignals();
        options.KeepAliveSeconds.Should().Be(15);
        options.KeepAlive.Should().Be(TimeSpan.FromSeconds(15));
    }
}

file static class SignalOptionsAssertions
{
    public static void SectionNameShouldBeSignals(this SignalOptions _) => SignalOptions.SectionName.Should().Be("Signals");
}
