using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// L'hôte nu, sans fake d'aucun module : celui des tests du socle lui-même.
// Il monte en plus le ProbeController, le déclencheur des tests de contrat
// HTTP — la sonde n'existe que dans cet hôte, jamais dans ceux des modules
// ni en production.
public sealed class SharedKernelWebAppFactory : IntegrationTestWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services => services
            .AddControllers()
            .AddApplicationPart(typeof(ProbeController).Assembly)
        );
    }
}
