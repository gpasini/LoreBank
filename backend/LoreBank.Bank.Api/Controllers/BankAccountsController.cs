using LoreBank.Bank.Application.Commands.CloseBankAccount;
using LoreBank.Bank.Application.Commands.DepositMoney;
using LoreBank.Bank.Application.Commands.OpenBankAccount;
using LoreBank.Bank.Application.Commands.WithdrawMoney;
using LoreBank.Bank.Application.Queries.GetBankAccountById;
using LoreBank.SharedKernel.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.Bank.Api.Controllers;

// CQS tenu jusqu'au bord HTTP : une action qui mute ne renvoie aucune
// représentation (201 + Location, ou 204), une action qui lit en renvoie une.
// Le geste est porté par ModuleController — SendAsync et CreateAsync n'
// acceptent qu'une commande, une lecture ne peut pas les emprunter.
//
// Le contrat HTTP est la surface Application (ADR 0012) : le body se lie
// directement sur la commande, une lecture sert son Result tel quel. Sur une
// route mixte, la route est autoritaire — le `with` écrase ce que le body
// aurait posté, et la propriété écrasée est [RouteBound] sur la commande pour
// que la Description OpenAPI ne la déclare pas dans le body.
//
// Les types de retour (CreationResult, CommandResult) sont l'affirmation que
// la Description lit — 201 + Location, 204 — sans [ProducesResponseType].
[Route("api/bank/accounts")]
public sealed class BankAccountsController(ISender sender) : ModuleController(sender)
{
    [HttpPost]
    public Task<CreationResult> Open(
        OpenBankAccountCommand command,
        CancellationToken cancellationToken
    ) => CreateAsync(
        command: command,
        actionName: nameof(GetById),
        cancellationToken: cancellationToken
    );

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankAccountResult>> GetById(
        Guid id,
        CancellationToken cancellationToken
    ) => await Sender.Send(
        request: new GetBankAccountByIdQuery(id),
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/deposits")]
    public Task<CommandResult> Deposit(
        Guid id,
        DepositMoneyCommand command,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: command with { AccountId = id },
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/withdrawals")]
    public Task<CommandResult> Withdraw(
        Guid id,
        WithdrawMoneyCommand command,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: command with { AccountId = id },
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/closure")]
    public Task<CommandResult> Close(
        Guid id,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: new CloseBankAccountCommand(id),
        cancellationToken: cancellationToken
    );
}
