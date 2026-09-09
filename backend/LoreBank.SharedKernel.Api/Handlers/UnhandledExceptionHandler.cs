using LoreBank.SharedKernel.Api.Problems;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LoreBank.SharedKernel.Api.Handlers;

// Dernier filet : tout ce qui n'est pas une DomainException sort d'ici en 500,
// avec la même forme que les erreurs métier et sans un mot de plus. L'exception
// part dans les logs — c'est le développeur qui la lit, pas le client.
//
// Enregistré dans le pipeline, pas dans MVC : contrairement au
// DomainExceptionFilter, il attrape aussi ce qui échoue avant d'atteindre une
// action.
public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    private const int Status = StatusCodes.Status500InternalServerError;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        logger.LogError(
            exception: exception,
            message: "Exception non gérée sur {Method} {Path}",
            args: [httpContext.Request.Method, httpContext.Request.Path]
        );

        httpContext.Response.StatusCode = Status;

        await httpContext.Response.WriteAsJsonAsync(
            value: ApiProblem.Create(
                httpContext: httpContext,
                status: Status
            ),
            options: null,
            contentType: ApiProblem.ContentTypeWithCharset,
            cancellationToken: cancellationToken
        );

        return true;
    }
}
