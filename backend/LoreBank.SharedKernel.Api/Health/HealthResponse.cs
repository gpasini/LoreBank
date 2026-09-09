using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LoreBank.SharedKernel.Api.Health;

// La forme de la réponse de Disponibilité (ADR 0022) : l'état global, et
// l'état de chaque module — les données que le check du socle rapporte, clé
// par clé, sous `modules`. C'est ce qu'un 503 doit dire à l'exploitant : pas
// seulement « indisponible », mais quel module. La Vivacité, elle, n'a rien à
// dire : le writer par défaut suffit.
public static class HealthResponse
{
    public static Task WriteAsync(
        HttpContext httpContext,
        HealthReport report
    )
    {
        var modules = report.Entries.Values
            .SelectMany(entry => entry.Data)
            .ToDictionary(
                keySelector: pair => pair.Key,
                elementSelector: pair => pair.Value
            );

        return httpContext.Response.WriteAsJsonAsync(new {
            status = report.Status.ToString(),
            modules,
        });
    }
}
