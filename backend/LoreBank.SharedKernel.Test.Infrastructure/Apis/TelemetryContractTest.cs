using System.Diagnostics;
using System.Text.Json;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Signals;
using OpenTelemetry.Metrics;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// La Télémétrie (ADR 0025) : ce que l'hôte exporte, relu sur l'exporteur
// mémoire du harnais, ancré sur le ProbeModule. Le socle prouve en BCL que
// les activités et les jauges existent (OutboxProcessorTest) ; ici on prouve
// que la composition de l'hôte les laisse sortir — une source ou un meter
// absents de la composition ne casseraient rien d'autre.
[TestFixture]
public sealed class TelemetryContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [SetUp]
    public async Task SetUp() => await ProbeOutbox.CleanAsync(Factory);

    [TearDown]
    public async Task TearDown() => await ProbeOutbox.CleanAsync(Factory);

    [Test]
    public async Task Get_ShouldExportAServerSpan_WithItsNpgsqlSpanBeneath()
    {
        // Arrange

        using var client = ClientFor(out var traceId);

        // Act

        var response = await client.GetAsync("api/probe/things");

        // Assert — le span serveur porte statut et route (l'instrumentation
        // ASP.NET Core, pas le natif de .NET 10), et la requête SQL est un
        // span « Npgsql » de la même trace, sous lui.

        response.EnsureSuccessStatusCode();

        var server = await ServerSpanAsync(traceId);

        server.GetTagItem("http.response.status_code").Should().Be(200);
        server.GetTagItem("http.route").Should().Be("api/probe/things");

        ProbeTelemetry.Activities
            .Should().ContainSingle(activity => activity.Source.Name == "Npgsql" && activity.TraceId == traceId)
            .Which.ParentSpanId.Should().Be(server.SpanId);
    }

    [Test]
    public async Task Get_ShouldExportTheServerSpan_UnderTheTraceIdTheErrorResponseCarries()
    {
        // Arrange

        using var client = Factory.CreateClient();

        // Act

        var response = await client.GetAsync("api/probe/crash");

        // Assert — Corrélation et Télémétrie sont la même trace.

        var traceId = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("traceId").GetString();

        var server = await ServerSpanAsync(ActivityTraceId.CreateFromString(traceId));

        server.TraceId.ToHexString().Should().Be(traceId);
        server.GetTagItem("http.response.status_code").Should().Be(500);
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldExportTheHandlerSpan_InTheTraceOfThePostThatPublished()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        using var client = ClientFor(out var traceId);

        var response = await client.PostAsync(
            requestUri: "api/probe/publications",
            content: null
        );

        response.EnsureSuccessStatusCode();

        var post = await ServerSpanAsync(traceId);

        // Act

        await Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

        // Assert — la livraison est exportée sous la trace du POST : le chemin
        // publieur → consommateur se lit d'un bloc dans le collecteur.

        var handled = ProbeTelemetry.Activities
            .Where(activity => activity.Source.Name == OutboxTracing.SourceName)
            .ToList();

        handled.Should().HaveCount(2).And.AllSatisfy(activity => {
                activity.OperationName.Should().Be("process probe.probe-happened");
                activity.TraceId.Should().Be(post.TraceId);
                activity.ParentSpanId.Should().Be(post.SpanId);
            }
        );
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldExportNoNpgsqlSpan_WhenThePassDeliversNothing()
    {
        // Arrange — l'outbox du Probe est vide (SetUp) : la passe ne fait que
        // réserver et mesurer, du SQL hors de toute trace.

        ProbeTelemetry.Reset();

        // Act

        await Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

        // Assert — le tick n'est pas un fait d'intérêt (ADR 0025) : un span
        // SQL sans requête ni handler au-dessus ne sort pas, sinon chaque
        // passe déposerait ses traces racines dans le collecteur.

        ProbeTelemetry.Activities.Should().NotContain(activity => activity.Source.Name == "Npgsql");
    }

    [Test]
    public async Task ProcessPendingAsync_ShouldExportTheOutboxGauges_AndTheNpgsqlMeter()
    {
        // Arrange

        ProbeFailingIntegrationEventHandler.ShouldFail = false;

        // Act

        await Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

        ProbeTelemetry.CollectMetrics(Factory.Services);

        // Assert

        var names = ProbeTelemetry.Metrics.Select(metric => metric.Name).ToList();

        names.Should().Contain(OutboxMetrics.PendingGauge).And.Contain(OutboxMetrics.PoisonedGauge);

        ProbeTelemetry.Metrics
            .Should().Contain(metric => metric.Name == OutboxMetrics.PendingGauge)
            .Which.MetricPoints.Should().Contain(point => HasModuleTag(
                    point: point,
                    module: "Probe"
                )
            );

        ProbeTelemetry.Metrics.Should().Contain(metric => metric.MeterName == "Npgsql");

        // Le Meter des Signaux (ADR 0026) sort par le même chemin.
        names.Should().Contain(SignalMetrics.SubscribersGauge);
    }

    // Un client dont chaque requête porte un traceparent neuf : le test ne
    // regarde que les spans de sa trace, jamais ceux d'une requête voisine.
    private static HttpClient ClientFor(out ActivityTraceId traceId)
    {
        var (header, id) = ProbeTelemetry.NewTraceParent();
        var client = Factory.CreateClient();

        client.DefaultRequestHeaders.Add(
            name: "traceparent",
            value: header
        );

        traceId = id;

        return client;
    }

    // Le span serveur se termine après l'envoi de la réponse : on l'attend,
    // brièvement, plutôt que de le supposer déjà exporté.
    private static async Task<Activity> ServerSpanAsync(ActivityTraceId traceId)
    {
        for (var attempt = 0; attempt < 50; attempt++) {
            var server = ProbeTelemetry.Activities
                .SingleOrDefault(activity => activity.Kind == ActivityKind.Server && activity.TraceId == traceId);

            if (server is not null) {
                return server;
            }

            await Task.Delay(20);
        }

        throw new InvalidOperationException($"Aucun span serveur exporté pour la trace {traceId}.");
    }

    private static bool HasModuleTag(
        MetricPoint point,
        string module
    )
    {
        foreach (var tag in point.Tags) {
            if (tag.Key == OutboxMetrics.ModuleTag && Equals(
                    objA: tag.Value,
                    objB: module
                )) {
                return true;
            }
        }

        return false;
    }
}
