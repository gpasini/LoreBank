using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Queries;
using LoreBank.Bank.Application.Results;
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
// aurait posté.
[Route("api/bank/accounts")]
public sealed class BankAccountsController(ISender sender) : ModuleController(sender)
{
    [HttpPost]
    public Task<ActionResult> Open(
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
    public Task<ActionResult> Deposit(
        Guid id,
        DepositMoneyCommand command,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: command with { AccountId = id },
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/withdrawals")]
    public Task<ActionResult> Withdraw(
        Guid id,
        WithdrawMoneyCommand command,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: command with { AccountId = id },
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/closure")]
    public Task<ActionResult> Close(
        Guid id,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: new CloseBankAccountCommand(id),
        cancellationToken: cancellationToken
    );
}
