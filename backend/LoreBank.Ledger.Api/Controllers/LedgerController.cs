using LoreBank.Ledger.Application.Queries.ListLedgerMovements;
using LoreBank.SharedKernel.Api.Controllers;
using LoreBank.SharedKernel.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LoreBank.Ledger.Api.Controllers;

// Le Ledger n'expose aucune commande : ses écritures naissent des integration
// events de Bank, jamais d'un POST — le bord HTTP du module est une surface
// de lecture pure. Une Liste sous une ressource (ADR 0027) : la route est
// autoritaire sur le compte, la query string porte le reste.
[Route("api/ledger/bank-accounts")]
public sealed class LedgerController(ISender sender) : ModuleController(sender)
{
    [HttpGet("{id:guid}/movements")]
    public async Task<ActionResult<ListPage<LedgerMovementResult>>> ListMovements(
        Guid id,
        [FromQuery] ListLedgerMovementsQuery query,
        CancellationToken cancellationToken
    ) => await Sender.Send(
        request: query with { AccountId = id },
        cancellationToken: cancellationToken
    );
}
