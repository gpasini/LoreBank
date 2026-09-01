using LoreBank.Bank.Application.Results;

namespace LoreBank.Bank.Api.Contracts;

public sealed record BankAccountResponse(
    Guid Id,
    string Iban,
    decimal Balance,
    string Currency,
    bool IsClosed
)
{
    public static BankAccountResponse From(BankAccountResult result) => new(
        Id: result.Id,
        Iban: result.Iban,
        Balance: result.Balance,
        Currency: result.Currency,
        IsClosed: result.IsClosed
    );
}
