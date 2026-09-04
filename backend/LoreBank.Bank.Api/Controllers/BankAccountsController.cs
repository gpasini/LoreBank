using LoreBank.Bank.Api.Contracts;
using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Application.Queries;
using LoreBank.SharedKernel.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.Bank.Api.Controllers;

// CQS tenu jusqu'au bord HTTP : une action qui mute ne renvoie aucune
// représentation (201 + Location, ou 204), une action qui lit en renvoie une.
// Le geste est porté par ModuleController — SendAsync et CreateAsync n'
// acceptent qu'une commande, une lecture ne peut pas les emprunter.
[Route("api/bank/accounts")]
public sealed class BankAccountsController(ISender sender) : ModuleController(sender)
{
    [HttpPost]
    public Task<ActionResult> Open(
        OpenBankAccountRequest request,
        CancellationToken cancellationToken
    ) => CreateAsync(
        command: new OpenBankAccountCommand(
            Iban: request.Iban,
            Currency: request.Currency
        ),
        actionName: nameof(GetById),
        cancellationToken: cancellationToken
    );

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankAccountResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        var result = await Sender.Send(
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
    ) => SendAsync(
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
    ) => SendAsync(
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
    ) => SendAsync(
        command: new CloseBankAccountCommand(id),
        cancellationToken: cancellationToken
    );
}
