using System.Net;
using System.Text.Json;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// L'Acteur (ADR 0023) sur HTTP : le template livré n'authentifie rien, donc
// toute requête est Anonyme. Le cas « un schéma est monté » se prouve sur
// l'implémentation en isolation (HttpContextActorTest), pas ici — monter un
// schéma de test serait le premier morceau du seam qu'on a décidé de ne pas
// construire.
[TestFixture]
public sealed class ActorContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [Test]
    public async Task Get_ShouldBeAnonymous_WhenNoAuthenticationSchemeIsMounted()
    {
        // Arrange

        using var client = Factory.CreateClient();

        // Act

        var response = await client.GetAsync("api/probe/actor");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        body.GetProperty("anonymous").GetBoolean().Should().BeTrue();
        body.GetProperty("id").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
