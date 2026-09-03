using LoreBank.Bank.Domain.Services;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Infrastructure.Fakes;

// Singleton dans l'hôte de test : les tests l'inspectent et le pilotent, et
// BankWebAppFactory.ResetFakes le réinitialise entre chaque test (appelé par
// BaseHostTest au SetUp comme au TearDown — point unique, plus de reset à
// recopier par fixture).
//
// État mutable partagé par toute la suite, sans aucune synchronisation : ça ne
// tient que parce que NUnit exécute en série — hypothèse déclarée au runner
// par le [assembly: Parallelizable(ParallelScope.None)] de GlobalUsings.cs.
public sealed class ConfigurableWelcomeLetterSender : IWelcomeLetterSender
{
    private readonly List<Iban> _sent = [];

    public IReadOnlyList<Iban> Sent => _sent;

    public Exception? ThrowOnSend { get; set; }

    public Task SendAsync(
        Iban iban,
        CancellationToken cancellationToken
    )
    {
        if (ThrowOnSend is not null) {
            throw ThrowOnSend;
        }

        _sent.Add(iban);

        return Task.CompletedTask;
    }

    public void Reset()
    {
        _sent.Clear();
        ThrowOnSend = null;
    }
}
