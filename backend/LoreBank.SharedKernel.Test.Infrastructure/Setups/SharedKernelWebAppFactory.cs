using Autofac;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// L'hôte nu, sans fake d'aucun module : celui des tests du socle lui-même.
// Il monte en plus le ProbeController, le déclencheur des tests de contrat
// HTTP — la sonde n'existe que dans cet hôte, jamais dans ceux des modules
// ni en production — et les sondes d'integration events, ancrées sur le
// module Bank comme les sondes de data migration.
public sealed class SharedKernelWebAppFactory : IntegrationTestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Retries resserrés pour que le marquage poison se prouve en deux
        // passes, et backoff nul pour qu'une ligne échouée soit ré-éligible
        // immédiatement — les tests pilotent les passes, pas l'horloge.
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> {
                    ["IntegrationEvents:MaxAttempts"] = "2",
                    ["IntegrationEvents:BackoffSeconds"] = "0",
                }
            )
        );

        builder.ConfigureTestServices(services => services
            .AddControllers()
            .AddApplicationPart(typeof(ProbeController).Assembly)
        );
    }

    // Les sondes se déclarent comme un module consommateur le ferait : une
    // registration par handler, ancrée sur Bank — son inbox journalisera.
    protected override void ConfigureModuleContainer(ContainerBuilder builder)
    {
        builder.RegisterInstance(new IntegrationEventHandlerRegistration(
            handlerType: typeof(ProbeRecordingIntegrationEventHandler),
            eventType: typeof(ProbeIntegrationEvent),
            moduleName: "Bank"
        ));
        builder.RegisterInstance(new IntegrationEventHandlerRegistration(
            handlerType: typeof(ProbeFailingIntegrationEventHandler),
            eventType: typeof(ProbeIntegrationEvent),
            moduleName: "Bank"
        ));
        builder.RegisterType<ProbeRecordingIntegrationEventHandler>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ProbeFailingIntegrationEventHandler>().AsSelf().InstancePerLifetimeScope();
    }

    public override void ResetFakes()
    {
        ProbeRecordingIntegrationEventHandler.Reset();
        ProbeFailingIntegrationEventHandler.Reset();
    }
}
