namespace LoreBank.Bank.Api.Contracts;

public sealed record OpenBankAccountRequest(
    string Iban,
    string Currency
);
