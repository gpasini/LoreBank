using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// Le dépileur unique de l'hôte, même géométrie que ModuleMigrator : un seul
// service pour toutes les outbox, les modules ne fournissent que leur
// contenu. Une passe qui échoue en bloc (base injoignable) est loggée et
// retentée à la cadence suivante — les échecs par event, eux, sont gérés
// ligne à ligne par le processor (backoff, poison).
public sealed class OutboxDispatcher(
    OutboxProcessor processor,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested) {
            try {
                await processor.ProcessPendingAsync(stoppingToken);
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
