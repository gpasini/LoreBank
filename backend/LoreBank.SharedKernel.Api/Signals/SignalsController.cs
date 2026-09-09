using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Application.Signals;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoreBank.SharedKernel.Api.Signals;

// La première route du socle côté clients (ADR 0026) : le flux global des
// Signaux, en SSE. Un controller du socle — pas d'un module — monté par
// l'hôte ; pas un ModuleController : il ne parle pas à ISender, il s'abonne
// au port ISignalStream. Le filtre ?resource=kind/guid est répétable, absent
// = tout ; un filtre mal formé est une erreur métier du socle
// (INVALID_SIGNAL_RESOURCE, 422 par DomainExceptionFilter). L'Acteur de la
// connexion est passé au flux : c'est lui que la policy consulte.
[ApiController]
[Route("api/signals")]
public sealed class SignalsController(
    ISignalStream stream,
    ICurrentActor currentActor,
    IOptions<SignalOptions> options
) : ControllerBase
{
    [HttpGet]
    public SignalStreamResult Subscribe(
        [FromQuery] string[] resource,
        CancellationToken cancellationToken
    ) =>
        new(
            signals: stream.SubscribeAsync(
                filter: SignalFilter.Parse(resource),
                actor: currentActor.Actor,
                cancellationToken: cancellationToken
            ),
            keepAlive: options.Value.KeepAlive
        );
}
