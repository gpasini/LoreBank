using LoreBank.Ledger.Application.Queries.GetBankAccountLedger;
using LoreBank.SharedKernel.Api.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.Ledger.Api.Controllers;

// Le Ledger n'expose aucune commande : ses écritures naissent des integration
// events de Bank, jamais d'un POST — le bord HTTP du module est une surface
// de lecture pure.
[Route("api/ledger/bank-accounts")]
public sealed class LedgerController(ISender sender) : ModuleController(sender)
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BankAccountLedgerResult>> GetByBankAccountId(
        Guid id,
        CancellationToken cancellationToken
    ) => await Sender.Send(
        request: new GetBankAccountLedgerQuery(id),
        cancellationToken: cancellationToken
    );
}
