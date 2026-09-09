using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoreBank.SharedKernel.Infrastructure.Modules;

// La Disponibilité (ADR 0022) : chaque base de module montée est joignable.
// Un seul check qui parcourt les IHostModule du conteneur à l'exécution — pas
// un check par entrée de HostModules.All à la composition : le harnais monte
// son ProbeModule après, et c'est sur lui que le socle se prouve (ADR 0017).
// Chaque module est rapporté dans les données, « Healthy » ou « Unhealthy »,
// pour qu'un 503 dise lequel. Les outbox n'entrent pas ici : une ligne en
// attente ou poison n'est pas une indisponibilité.
public sealed class ModuleDatabasesHealthCheck(
    IEnumerable<IHostModule> modules,
    IServiceProvider services
) : IHealthCheck
{
    public const string Name = "modules";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        var statuses = new Dictionary<string, object>();
        var healthy = true;

        foreach (var module in modules) {
            await using var scope = services.CreateAsyncScope();

            var canConnect = await ModuleDbContexts
                .Resolve(
                    services: scope.ServiceProvider,
                    module: module
                )
                .Database
                .CanConnectAsync(cancellationToken);

            statuses[module.ModuleName] = canConnect ? nameof(HealthStatus.Healthy) : nameof(HealthStatus.Unhealthy);
            healthy &= canConnect;
        }

        return new HealthCheckResult(
            status: healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy,
            description: null,
            exception: null,
            data: statuses
        );
    }
}
