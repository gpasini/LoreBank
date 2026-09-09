using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Api.Controllers;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Le déclencheur des tests de contrat HTTP du socle. Monté par
// SharedKernelWebAppFactory seulement — il n'existe dans aucun autre hôte.
//
// Deux familles d'actions. Les portes de sortie d'erreur (422 métier, 404
// introuvable, 409 version périmée, 400 de binding, 500 non gérée), pour
// ErrorContractTest — un
// throw direct suffit, DomainExceptionFilter est un filtre MVC, il voit la
// même chose qu'en sortie de handler MediatR. Et les formes que la
// Description OpenAPI doit lire (DescriptionContractTest) : commande,
// création, lecture, route mixte à propriété [RouteBound]. Ces dernières ne
// s'exécutent jamais — la Description se lit sans appel — donc aucun handler
// MediatR n'est câblé derrière. Et l'Acteur courant (ActorContractTest) : le
// template livré n'authentifie rien, une requête HTTP est Anonyme (ADR 0023).
// Enfin les sondes de Télémétrie (TelemetryContractTest) : une lecture qui
// touche la base du Probe — pour le span Npgsql sous le span serveur — et une
// publication sur son outbox — pour la trace qui traverse jusqu'au handler.
[Route("api/probe")]
public sealed class ProbeController(ISender sender) : ModuleController(sender)
{
    [HttpGet("domain-failure")]
    public IActionResult ThrowDomainFailure() => throw new ProbeFailureException("NOPE");

    [HttpGet("things/{thingId:guid}")]
    public IActionResult ThrowThingNotFound(Guid thingId) => throw new ProbeThingNotFoundException(thingId);

    [HttpPost("things/{thingId:guid}/concurrent-updates")]
    public IActionResult ThrowConcurrentUpdate(Guid thingId) => throw new ConcurrentUpdateException(thingId);

    [HttpPost("bindings")]
    public IActionResult Bind(ProbeBindingRequest request) => NoContent();

    [HttpGet("actor")]
    public ActionResult<ProbeActorResult> Actor([FromServices] ICurrentActor currentActor) => new ProbeActorResult(
        Anonymous: currentActor.Actor.IsAnonymous,
        Id: currentActor.Actor.IsAnonymous ? null : currentActor.Actor.Id
    );

    [HttpGet("crash")]
    public IActionResult Crash() => throw new InvalidOperationException("boom");

    [HttpPost("commands")]
    public Task<CommandResult> Command(
        ProbeCommand command,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: command,
        cancellationToken: cancellationToken
    );

    [HttpPost("creations")]
    public Task<CreationResult> Create(
        ProbeCreationCommand command,
        CancellationToken cancellationToken
    ) => CreateAsync(
        command: command,
        actionName: nameof(Read),
        cancellationToken: cancellationToken
    );

    [HttpGet("readings/{id:guid}")]
    public ActionResult<ProbeResult> Read(Guid id) => new ProbeResult(
        Id: id,
        Amount: 0m
    );

    [HttpGet("things")]
    public async Task<ActionResult<int>> CountThings(
        [FromServices] ProbeDbContext dbContext,
        CancellationToken cancellationToken
    ) => await dbContext.ProbeThings.CountAsync(cancellationToken);

    [HttpPost("publications")]
    public async Task<IActionResult> Publish(
        [FromServices] IIntegrationEventPublisher publisher,
        CancellationToken cancellationToken
    )
    {
        await publisher.PublishAsync(
            integrationEvent: new ProbeIntegrationEvent(
                ThingId: Guid.NewGuid(),
                Label: "publiée en HTTP"
            ),
            cancellationToken: cancellationToken
        );

        return NoContent();
    }

    [HttpPost("readings/{id:guid}/mixed")]
    public Task<CommandResult> Mix(
        Guid id,
        ProbeMixedCommand command,
        CancellationToken cancellationToken
    ) => SendAsync(
        command: command with { ThingId = id },
        cancellationToken: cancellationToken
    );
}
