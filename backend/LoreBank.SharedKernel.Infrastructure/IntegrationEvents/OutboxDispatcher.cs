using LoreBank.SharedKernel.Infrastructure.Signals;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Le dépileur unique de l'hôte, même géométrie que ModuleMigrator : un seul
// service pour toutes les outbox, les modules ne fournissent que leur
// contenu. Une passe qui échoue en bloc (base injoignable) est loggée et
// retentée à la cadence suivante — les échecs par event, eux, sont gérés
// ligne à ligne par le processor (backoff, poison). La purge de Rétention
// (ADR 0021) suit la même boucle à sa propre cadence : dès le démarrage,
// puis à l'intervalle — un hôte redémarré souvent purgerait sinon jamais.
// Le suiveur des Signaux (ADR 0026) passe après la livraison, à chaque
// passe : sur une instance seule, la ligne livrée par la passe est signalée
// par la même passe.
public sealed class OutboxDispatcher(
    OutboxProcessor processor,
    SignalTailer tailer,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var purgeCadence = new PurgeCadence(options.Value.PurgeInterval);

        while (!stoppingToken.IsCancellationRequested) {
            try {
                await processor.ProcessPendingAsync(stoppingToken);
                await tailer.TailAsync(stoppingToken);

                if (purgeCadence.IsDue(DateTimeOffset.UtcNow)) {
                    await processor.PurgeExpiredAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                return;
            }
            catch (Exception exception) {
                logger.LogError(
                    exception: exception,
                    message: "La passe de livraison des integration events a échoué ; nouvelle passe à la prochaine cadence."
                );
            }

            try {
                await Task.Delay(
                    delay: options.Value.PollingInterval,
                    cancellationToken: stoppingToken
                );
            }
            catch (OperationCanceledException) {
                return;
            }
        }
    }
}
