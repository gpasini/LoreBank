using LoreBank.Bank.Domain.Services;
using LoreBank.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LoreBank.Bank.Infrastructure.Services;

// Implémentation de référence du template : l'utilisateur qui clone la remplace
// par son fournisseur de courrier.
public sealed class LoggingWelcomeLetterSender(ILogger<LoggingWelcomeLetterSender> logger)
    : IWelcomeLetterSender
{
    public Task SendAsync(
        Iban iban,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Lettre de bienvenue envoyée au titulaire de l'IBAN {Iban}.",
            iban.Value
        );

        return Task.CompletedTask;
    }
}
