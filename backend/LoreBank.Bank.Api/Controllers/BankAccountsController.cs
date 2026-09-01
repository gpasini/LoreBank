using LoreBank.Bank.Api.Contracts;
using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.Bank.Api.Controllers;

// CQS tenu jusqu'au bord HTTP : une action qui mute ne renvoie aucune
// représentation (201 + Location, ou 204), une action qui lit en renvoie une.
// Le client qui veut l'état d'après fait un GET.
//
// C'est un aller-retour de plus, assumé : tant qu'une commande renvoie aussi la
// ressource, elle est également une lecture, et la représentation qu'elle sert
// peut diverger de celle du GET sans que rien ne le signale.
[ApiController]
[Route("api/bank/accounts")]
public sealed class BankAccountsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult> Open(
        OpenBankAccountRequest request,
        CancellationToken cancellationToken
    )
    {
        var accountId = await sender.Send(
            request: new OpenBankAccountCommand(
                Iban: request.Iban,
                Currency: request.Currency
            ),
            cancellationToken: cancellationToken
        );

        // Le seul retour admis à une commande — l'identifiant créé — sert ici à
        // construire Location, pas à renvoyer un corps.
        return CreatedAtAction(
            actionName: nameof(GetById),
            routeValues: new { id = accountId },
            value: null
        );
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankAccountResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var result = await sender.Send(
            request: new GetBankAccountByIdQuery(id),
            cancellationToken: cancellationToken
        );

        return BankAccountResponse.From(result);
    }

    [HttpPost("{id:guid}/deposits")]
    public Task<ActionResult> Deposit(
        Guid id,
        AmountRequest request,
        CancellationToken cancellationToken
    ) => ExecuteAsync(
        command: new DepositMoneyCommand(
            AccountId: id,
            Amount: request.Amount,
            Currency: request.Currency
        ),
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/withdrawals")]
    public Task<ActionResult> Withdraw(
        Guid id,
        AmountRequest request,
        CancellationToken cancellationToken
    ) => ExecuteAsync(
        command: new WithdrawMoneyCommand(
            AccountId: id,
            Amount: request.Amount,
            Currency: request.Currency
        ),
        cancellationToken: cancellationToken
    );

    [HttpPost("{id:guid}/closure")]
    public Task<ActionResult> Close(
        Guid id,
        CancellationToken cancellationToken
    ) => ExecuteAsync(
        command: new CloseBankAccountCommand(id),
        cancellationToken: cancellationToken
    );

    private async Task<ActionResult> ExecuteAsync(
        IRequest command,
        CancellationToken cancellationToken
    )
    {
        await sender.Send(
            request: command,
            cancellationToken: cancellationToken
        );

        return NoContent();
    }
}
