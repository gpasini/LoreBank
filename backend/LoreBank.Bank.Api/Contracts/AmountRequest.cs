namespace LoreBank.Bank.Api.Contracts;

public sealed record AmountRequest(
    decimal Amount,
    string Currency
);
