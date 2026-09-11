using LoreBank.SharedKernel.Api.Actors;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// Le garde-fou de l'opt-out : la factory du socle est le seul hôte de test
// qui garde l'implémentation réelle du port de l'Acteur, parce qu'elle est
// celle qui la prouve (ActorContractTest).
//
// Sans ce test, l'oubli serait silencieux : le fake du harnais rend Anonyme
// par défaut, exactement comme HttpContextActor sans schéma monté — le
// contrat continuerait de passer en ne prouvant plus rien.
[TestFixture]
[TestOf(typeof(IntegrationTestWebAppFactory))]
public sealed class ActorCompositionTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [Test]
    public void CurrentActor_ShouldBeTheRealImplementation_WhenTheHostIsTheSharedKernelOne()
    {
        // Arrange

        using var scope = Factory.Services.CreateScope();

        // Act

        var actor = scope.ServiceProvider.GetRequiredService<ICurrentActor>();

        // Assert

        actor.Should().BeOfType<HttpContextActor>();
    }
}
