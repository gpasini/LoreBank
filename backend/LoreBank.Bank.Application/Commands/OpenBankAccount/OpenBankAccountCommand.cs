using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Commands.OpenBankAccount;

public sealed record OpenBankAccountCommand(
    string Iban,
    string Currency
) : ICreationCommand;
