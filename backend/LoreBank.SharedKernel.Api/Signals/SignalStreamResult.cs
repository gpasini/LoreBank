using System.Text.Json;
using LoreBank.SharedKernel.Application.Signals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LoreBank.SharedKernel.Api.Signals;

// Le flux de Signaux au bord HTTP (ADR 0026) : une réponse 200 en
// text/event-stream qui ne se termine pas — chaque Signal est un event SSE
// (`data:` le Signal en JSON, la même forme que les Results), et un
// commentaire de keep-alive part à l'ouverture puis quand rien ne passe. Pas d'`id:` : pas de
// rejeu, un client reconnecté relit ce qu'il affiche. Pas d'`event:` non
// plus : le discriminant est dans le Signal — un EventSource n'a pas
// d'écouteur générique pour les events nommés, et le front n'a aucun
// discriminant à écrire pour tout recevoir.
// Un type du socle plutôt qu'un IActionResult nu, comme CommandResult : le
// type de retour de l'action affirme ce qu'elle sert, et c'est cette
// affirmation que la Description lit (DescriptionConvention).
public sealed class SignalStreamResult(
    IAsyncEnumerable<Signal> signals,
    TimeSpan keepAlive
) : IActionResult
{
    public const string ContentType = "text/event-stream";

    private const string KeepAliveComment = ": keep-alive\n\n";

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        var cancellationToken = context.HttpContext.RequestAborted;
        var json = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>()
            .Value
            .JsonSerializerOptions;

        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = $"{ContentType}; charset=utf-8";
        response.Headers.CacheControl = "no-cache";

        // Un proxy ou le serveur lui-même ne doivent rien retenir : chaque
        // event part au client dès qu'il est écrit.
        response.Headers["X-Accel-Buffering"] = "no";
        context.HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

        var enumerator = signals.GetAsyncEnumerator(cancellationToken);
        var next = enumerator.MoveNextAsync().AsTask();

        try {
            // StartAsync fige les en-têtes ; c'est le premier octet du corps
            // qui les met sur le fil — Kestrel comme un proxy Node (celui de
            // Vite) les retiennent jusque-là, et le navigateur n'ouvrirait le
            // flux qu'au premier keep-alive. Le premier commentaire part donc
            // tout de suite.
            await response.StartAsync(cancellationToken);
            await WriteAsync(
                response: response,
                text: KeepAliveComment,
                cancellationToken: cancellationToken
            );

            while (true) {
                bool hasNext;

                try {
                    hasNext = await next.WaitAsync(
                        timeout: keepAlive,
                        cancellationToken: cancellationToken
                    );
                }
                catch (TimeoutException) {
                    await WriteAsync(
                        response: response,
                        text: KeepAliveComment,
                        cancellationToken: cancellationToken
                    );

                    continue;
                }

                if (!hasNext) {
                    return;
                }

                await WriteAsync(
                    response: response,
                    text: Format(
                        signal: enumerator.Current,
                        json: json
                    ),
                    cancellationToken: cancellationToken
                );

                next = enumerator.MoveNextAsync().AsTask();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            // Le client est parti : la fin normale d'un flux.
        }
        finally {
            // Un itérateur ne se dispose pas pendant qu'un MoveNextAsync est
            // en vol : on attend le sien — il s'annule avec le même jeton —
            // avant de le rendre.
            await SettleAsync(next);
            await enumerator.DisposeAsync();
        }
    }

    private static async Task SettleAsync(Task<bool> next)
    {
        try {
            await next;
        }
        catch (Exception) {
            // Annulé ou échoué : ce qui compte est qu'il soit terminé.
        }
    }

    public static string Format(
        Signal signal,
        JsonSerializerOptions json
    ) =>
        $"data: {JsonSerializer.Serialize(
            value: signal,
            options: json
        )}\n\n";

    private static async Task WriteAsync(
        HttpResponse response,
        string text,
        CancellationToken cancellationToken
    )
    {
        await response.WriteAsync(
            text: text,
            cancellationToken: cancellationToken
        );
        await response.Body.FlushAsync(cancellationToken);
    }
}
