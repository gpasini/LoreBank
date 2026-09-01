using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Domain.Services;

public interface IWelcomeLetterSender
{
    Task SendAsync(
        Iban iban,
        CancellationToken cancellationToken
    );
}
