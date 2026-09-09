using Autofac;
using LoreBank.Probe.Infrastructure;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// L'hôte nu, sans fake d'aucun module : celui des tests du socle lui-même.
// Il monte en plus le ProbeController, le déclencheur des tests de contrat
// HTTP — la sonde n'existe que dans cet hôte, jamais dans ceux des modules
// ni en production — et le ProbeModule (ADR 0017), le module-terrain sur
// lequel toutes les sondes s'ancrent : le socle se prouve sans dépendre
// d'un module d'exemple supprimable.
public sealed class SharedKernelWebAppFactory : IntegrationTestWebAppFactory
{
    private readonly IHostModule[] _additionalModules = [new ProbeModule()];

    public override IReadOnlyCollection<IHostModule> AdditionalModules => _additionalModules;

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

        // La sonde de logs, à côté des providers de l'hôte : les tests relisent
        // la Corrélation et les synthèses d'outbox dans ses entrées.
        builder.ConfigureLogging(logging => logging.AddProvider(ProbeLogs.Provider));
    }

    // Les sondes se déclarent comme un module consommateur le ferait : une
    // registration par handler, ancrée sur le ProbeModule — son inbox
    // journalisera.
    protected override void ConfigureModuleContainer(ContainerBuilder builder)
    {
        builder.RegisterInstance(new IntegrationEventHandlerRegistration(
            handlerType: typeof(ProbeRecordingIntegrationEventHandler),
            eventType: typeof(ProbeIntegrationEvent),
            moduleName: "Probe"
        ));
        builder.RegisterInstance(new IntegrationEventHandlerRegistration(
            handlerType: typeof(ProbeFailingIntegrationEventHandler),
            eventType: typeof(ProbeIntegrationEvent),
            moduleName: "Probe"
        ));
        builder.RegisterType<ProbeRecordingIntegrationEventHandler>().AsSelf().InstancePerLifetimeScope();
        builder.RegisterType<ProbeFailingIntegrationEventHandler>().AsSelf().InstancePerLifetimeScope();
    }

    public override void ResetFakes()
    {
        base.ResetFakes();
        ProbeRecordingIntegrationEventHandler.Reset();
        ProbeFailingIntegrationEventHandler.Reset();
        ProbeLogs.Reset();
    }
}
