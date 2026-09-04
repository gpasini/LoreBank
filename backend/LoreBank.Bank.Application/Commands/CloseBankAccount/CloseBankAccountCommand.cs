using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Commands.CloseBankAccount;

public sealed record CloseBankAccountCommand(Guid AccountId) : ICommand;
