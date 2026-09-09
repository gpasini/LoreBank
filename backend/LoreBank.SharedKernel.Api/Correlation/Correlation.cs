using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace LoreBank.SharedKernel.Api.Correlation;

// La Corrélation (ADR 0022) : l'identifiant de trace W3C que le hosting pose
// sur chaque requête — et propage depuis un traceparent entrant — lu ici une
// seule fois pour tout ce qui le cite. À défaut d'Activity (un hôte sans
// diagnostics), l'identifiant de requête du framework, pour ne jamais
// répondre sans poignée.
public static class Correlation
{
    public static string Of(HttpContext httpContext) =>
        Activity.Current?.TraceId.ToHexString() ?? httpContext.TraceIdentifier;
}
