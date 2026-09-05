namespace LoreBank.Bank.Contracts.Readers;

// Ce qu'un autre module a le droit de savoir d'un compte : de quoi l'étiqueter.
public sealed record BankAccountSummary(
    Guid Id,
    string Iban
);
