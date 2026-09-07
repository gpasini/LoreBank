using LoreBank.SharedKernel.Application;

namespace LoreBank.Bank.Application.Commands.DepositMoney;

public sealed record DepositMoneyCommand(
    [property: RouteBound] Guid AccountId,
    decimal Amount,
    string Currency
) : ICommand;
