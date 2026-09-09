using System.Security.Claims;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Domain.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace LoreBank.SharedKernel.Api.Actors;

// L'implémentation du socle du port ICurrentActor (ADR 0023) : elle lit le
// principal qu'ASP.NET Core expose de toute façon — Anonyme sans schéma
// monté, l'identifiant du principal dès que le cloneur monte le sien. Le
// socle n'authentifie rien : il ne fait que lire ce qu'un schéma aura posé.
public sealed class HttpContextActor(IHttpContextAccessor httpContextAccessor) : ICurrentActor
{
    // Le `sub` d'un jeton, que le mapping entrant par défaut de JwtBearer
    // renomme en NameIdentifier — d'où les deux, dans cet ordre.
    private const string SubjectClaim = "sub";

    public Actor Actor => ActorOf(httpContextAccessor.HttpContext?.User);

    private static Actor ActorOf(ClaimsPrincipal? principal)
    {
        if (principal?.Identity is not { IsAuthenticated: true }) {
            return Actor.Anonymous;
        }

        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue(SubjectClaim);

        // Authentifié sans identifiant : le schéma du cloneur est mal
        // configuré. Pas un Anonyme — un fait enregistré sous une fausse
        // identité serait pire qu'un 500.
        return id is null
            ? throw new InvalidOperationException(
                $"Le principal est authentifié mais ne porte ni {ClaimTypes.NameIdentifier} ni {SubjectClaim} : l'Acteur ne peut pas être établi."
            )
            : Actor.Of(id);
    }
}
