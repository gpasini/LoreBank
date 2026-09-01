using LoreBank.Bank.Domain.Services;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Unit.Fakes;

public sealed class FakeWelcomeLetterSender : IWelcomeLetterSender
{
    public Iban? SentTo { get; private set; }

    public Task SendAsync(
        Iban iban,
        CancellationToken cancellationToken
    )
    {
        SentTo = iban;

        return Task.CompletedTask;
    }
}
