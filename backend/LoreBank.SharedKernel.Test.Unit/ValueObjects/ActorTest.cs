using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Unit.ValueObjects;

[TestFixture]
[TestOf(typeof(Actor))]
public sealed class ActorTest
{
    [Test]
    public void Of_ShouldKeepTheIdentifier_WhenValueIsValid()
    {
        var actor = Actor.Of("alice");

        actor.IsAnonymous.Should().BeFalse();
        actor.Id.Should().Be("alice");
    }

    [Test]
    public void Of_ShouldTrim_WhenValueHasSurroundingSpaces()
    {
        Actor.Of("  alice ").Id.Should().Be("alice");
    }

    [TestCase("")]
    [TestCase("   ")]
    public void Of_ShouldThrow_WhenValueIsBlank(string value)
    {
        var act = () => Actor.Of(value);

        act.Should().Throw<InvalidActorException>()
            .Which.Should().Match<InvalidActorException>(exception =>
                exception.Code == "INVALID_ACTOR"
                && exception.Parameters["actor"].Equals(value)
            );
    }

    [Test]
    public void Anonymous_ShouldBeAnonymous()
    {
        Actor.Anonymous.IsAnonymous.Should().BeTrue();
    }

    [Test]
    public void Id_ShouldThrow_WhenActorIsAnonymous()
    {
        var act = () => Actor.Anonymous.Id;

        act.Should().Throw<InvalidOperationException>();
    }

    [Test]
    public void Equals_ShouldBeTrue_WhenIdentifiersMatch()
    {
        Actor.Of("alice").Should().Be(Actor.Of("alice"));
        Actor.Of("alice").Should().NotBe(Actor.Of("bob"));
        Actor.Of("alice").Should().NotBe(Actor.Anonymous);
    }

    // La réhydratation truste la base (ADR 0016) : null est l'Anonyme, toute
    // autre valeur est reprise telle quelle.
    [Test]
    public void Hydrate_ShouldBeAnonymous_WhenStoredValueIsNull()
    {
        Actor.Hydrate(null).Should().Be(Actor.Anonymous);
    }

    [Test]
    public void Hydrate_ShouldAcceptTheStoredValueAsIs()
    {
        Actor.Hydrate(" alice ").Id.Should().Be(" alice ");
        Actor.Hydrate("alice").Should().Be(Actor.Of("alice"));
    }
}
