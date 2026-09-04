using Microsoft.AspNetCore.Mvc;

namespace LoreBank.SharedKernel.Test.Infrastructure.Probes;

// Le déclencheur des tests de contrat HTTP du socle : une action par porte de
// sortie (422 métier, 404 introuvable, 400 de binding, 500 non gérée). Monté
// par SharedKernelWebAppFactory seulement — il n'existe dans aucun autre hôte.
// Un throw direct suffit : DomainExceptionFilter est un filtre MVC, il voit la
// même chose qu'en sortie de handler MediatR.
[ApiController]
[Route("api/probe")]
public sealed class ProbeController : ControllerBase
{
    [HttpGet("domain-failure")]
    public IActionResult ThrowDomainFailure() => throw new ProbeFailureException("NOPE");

    [HttpGet("things/{thingId:guid}")]
    public IActionResult ThrowThingNotFound(Guid thingId) => throw new ProbeThingNotFoundException(thingId);

    [HttpPost("bindings")]
    public IActionResult Bind(ProbeBindingRequest request) => NoContent();

    [HttpGet("crash")]
    public IActionResult Crash() => throw new InvalidOperationException("boom");
}
