using LoreBank.Bank.Domain.Services;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.Bank.Test.Infrastructure.Fakes;

// Singleton dans l'hôte de test : les tests l'inspectent et le pilotent, et le
// réinitialisent au SetUp pour ne pas se contaminer entre eux.
//
// État mutable partagé par toute la suite, sans aucune synchronisation : ça ne
// tient que parce que NUnit exécute ici en série. Activer le parallélisme au
// niveau assembly casserait la suite d'une façon qui ressemblerait à de la
// flakiness (des tests qui se marchent dessus), pas à une erreur évidente.
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
