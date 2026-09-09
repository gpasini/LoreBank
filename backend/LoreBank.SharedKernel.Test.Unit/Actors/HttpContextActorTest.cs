using System.Security.Claims;
using LoreBank.SharedKernel.Api.Actors;
using LoreBank.SharedKernel.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace LoreBank.SharedKernel.Test.Unit.Actors;

[TestFixture]
[TestOf(typeof(HttpContextActor))]
public sealed class HttpContextActorTest
{
    [Test]
    public void Actor_ShouldBeAnonymous_WhenThereIsNoHttpContext()
    {
        new HttpContextActor(new HttpContextAccessor()).Actor.Should().Be(Actor.Anonymous);
    }

    [Test]
    public void Actor_ShouldBeAnonymous_WhenThePrincipalIsNotAuthenticated()
    {
        ActorOf(new ClaimsPrincipal(new ClaimsIdentity())).Should().Be(Actor.Anonymous);
    }

    [Test]
    public void Actor_ShouldBeIdentified_WhenThePrincipalCarriesANameIdentifier()
    {
        ActorOf(Authenticated(new Claim(
                    type: ClaimTypes.NameIdentifier,
                    value: "alice"
                )
            )
        ).Should().Be(Actor.Of("alice"));
    }

    [Test]
    public void Actor_ShouldBeIdentified_WhenThePrincipalCarriesASubjectOnly()
    {
        ActorOf(Authenticated(new Claim(
                    type: "sub",
                    value: "bob"
                )
            )
        ).Should().Be(Actor.Of("bob"));
    }

    [Test]
    public void Actor_ShouldPreferTheNameIdentifier_WhenThePrincipalCarriesBoth()
    {
        ActorOf(Authenticated(
                new Claim(
                    type: "sub",
                    value: "bob"
                ),
                new Claim(
                    type: ClaimTypes.NameIdentifier,
                    value: "alice"
                )
            )
        ).Should().Be(Actor.Of("alice"));
    }

    // Authentifié sans identifiant : une erreur de configuration du schéma,
    // jamais un Anonyme — un 500 franc plutôt qu'un fait enregistré sous une
    // fausse identité.
    [Test]
    public void Actor_ShouldThrow_WhenThePrincipalIsAuthenticatedWithoutAnyIdentifier()
    {
        var act = () => ActorOf(Authenticated(new Claim(
                    type: ClaimTypes.Email,
                    value: "alice@example.test"
                )
            )
        );

        act.Should().Throw<InvalidOperationException>()
            .Which.Message.Should().Contain(ClaimTypes.NameIdentifier).And.Contain("sub");
    }

    private static Actor ActorOf(ClaimsPrincipal principal)
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } };

        return new HttpContextActor(accessor).Actor;
    }

    private static ClaimsPrincipal Authenticated(params Claim[] claims) => new(new ClaimsIdentity(
            claims: claims,
            authenticationType: "Test"
        )
    );
}
