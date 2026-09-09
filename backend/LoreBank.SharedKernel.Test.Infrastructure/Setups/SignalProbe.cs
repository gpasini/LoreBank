using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using LoreBank.SharedKernel.Infrastructure.Signals;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La surface d'observation des Signaux offerte aux modules (ADR 0026) : le
// garde-fou « mes jumeaux signalent vraiment » se réduit à ouvrir le flux,
// agir en HTTP, faire passer le processor puis le suiveur, et lire le
// Signal. Le suiveur se pilote à la main comme le processor — la cadence
// de fond est neutralisée par le harnais — et s'amorce au premier passage
// (son curseur naît là : un test amorce avant d'agir).
public static class SignalProbe
{
    public static Task TailAsync(IntegrationTestWebAppFactory factory) =>
        factory.Services
            .GetRequiredService<SignalTailer>()
            .TailAsync(CancellationToken.None);

    // Ouvre le flux et attend ses en-têtes : à son retour, l'abonnement est
    // pris côté serveur — ce qui est publié ensuite arrivera.
    public static async Task<Stream> OpenAsync(
        HttpClient client,
        string query = ""
    )
    {
        var cancellation = new CancellationTokenSource();
        var response = await client.GetAsync(
            requestUri: $"api/signals{query}",
            completionOption: HttpCompletionOption.ResponseHeadersRead,
            cancellationToken: cancellation.Token
        );

        return new Stream(
            response: response,
            reader: new StreamReader(await response.Content.ReadAsStreamAsync(cancellation.Token)),
            cancellation: cancellation
        );
    }


    public sealed class Stream(
        HttpResponseMessage response,
        StreamReader reader,
        CancellationTokenSource cancellation
    ) : IAsyncDisposable
    {
        public HttpStatusCode StatusCode => response.StatusCode;

        public MediaTypeHeaderValue? ContentType => response.Content.Headers.ContentType;

        public Task<string?> ReadLineAsync(TimeSpan timeout) =>
            reader.ReadLineAsync(cancellation.Token).AsTask().WaitAsync(timeout);

        // Le prochain Signal : les commentaires (keep-alive) sont sautés, le
        // champ data: lu jusqu'à la ligne vide qui clôt l'event.
        public async Task<JsonElement> ReadSignalAsync(TimeSpan timeout)
        {
            string? data = null;

            while (true) {
                var line = await ReadLineAsync(timeout)
                           ?? throw new InvalidOperationException("Le flux de Signaux s'est terminé.");

                if (line.StartsWith(':')) {
                    continue;
                }

                if (line.Length == 0) {
                    if (data is not null) {
                        return JsonDocument.Parse(data).RootElement;
                    }

                    continue;
                }

                if (line.StartsWith("data: ")) {
                    data = line["data: ".Length..];
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellation.CancelAsync();
            reader.Dispose();
            response.Dispose();
            cancellation.Dispose();
        }
    }
}
