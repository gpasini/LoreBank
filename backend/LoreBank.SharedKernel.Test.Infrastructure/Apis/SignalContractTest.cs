using System.Net;
using System.Text.Json;
using LoreBank.SharedKernel.Api.Problems;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// Le flux de Signaux au bord HTTP (ADR 0026), prouvé sur le terrain probe :
// un GET qui ne se termine pas, en text/event-stream, qui garde la
// connexion en vie, pousse un Signal une fois la ligne livrée et suivie,
// filtre par ressource, consulte la policy, et refuse un filtre mal formé
// avec la forme d'erreur du socle. Hérite de BaseHostTest : le flux
// traverse vraiment HTTP.
[TestFixture]
public sealed class SignalContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private readonly static TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private HttpClient _client = null!;

    [SetUp]
    public async Task SetUp()
    {
        _client = Factory.CreateClient();
        ProbeFailingIntegrationEventHandler.ShouldFail = false;
        await ProbeOutbox.CleanAsync(Factory);
        await SignalProbe.TailAsync(Factory);
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await ProbeOutbox.CleanAsync(Factory);
    }

    [Test]
    public async Task Subscribe_ShouldServeAnEventStream_ThatKeepsAlive()
    {
        // Act

        await using var stream = await SignalProbe.OpenAsync(_client);

        // Assert — le keep-alive du harnais est resserré à 200 ms.

        stream.StatusCode.Should().Be(HttpStatusCode.OK);
        stream.ContentType!.MediaType.Should().Be("text/event-stream");
        stream.ContentType.CharSet.Should().Be("utf-8");

        (await stream.ReadLineAsync(Timeout)).Should().Be(": keep-alive");
    }

    [Test]
    public async Task Subscribe_ShouldPushTheSignal_OnceTheRowIsDispatchedAndTailed()
    {
        // Arrange

        var thingId = Guid.NewGuid();

        await using var stream = await SignalProbe.OpenAsync(_client);

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: thingId,
                Label: "en flux"
            )
        );

        // Act

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        // Assert — un event SSE, le Signal en data.

        var signal = await stream.ReadSignalAsync(Timeout);

        signal.GetProperty("discriminant").GetString().Should().Be("probe.probe-happened");
        signal.GetProperty("resourceKind").GetString().Should().Be(ProbeIntegrationEvent.ThingResourceKind);
        signal.GetProperty("resourceId").GetGuid().Should().Be(thingId);
        signal.GetProperty("occurredAt").GetDateTimeOffset().Should().BeCloseTo(
            nearbyTime: DateTimeOffset.UtcNow,
            precision: TimeSpan.FromMinutes(1)
        );
        signal.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "discriminant",
            "resourceKind",
            "resourceId",
            "occurredAt"
        );
    }

    [Test]
    public async Task Subscribe_ShouldPushOnlyTheListedResources_WhenAFilterIsGiven()
    {
        // Arrange — un abonné à une seule chose ; une autre chose d'abord.

        var wanted = Guid.NewGuid();
        var other = Guid.NewGuid();

        await using var stream = await SignalProbe.OpenAsync(
            client: _client,
            query: $"?resource={ProbeIntegrationEvent.ThingResourceKind}/{wanted}"
        );

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: other,
                Label: "pas elle"
            )
        );
        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: wanted,
                Label: "elle"
            )
        );

        // Act

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        // Assert — le premier Signal lu est celui du filtre.

        (await stream.ReadSignalAsync(Timeout)).GetProperty("resourceId").GetGuid().Should().Be(wanted);
    }

    [Test]
    public async Task Subscribe_ShouldPushOnlyWhatThePolicyAdmits()
    {
        // Arrange — la policy du harnais ne laisse passer qu'une chose.

        var admitted = Guid.NewGuid();

        Factory.SignalPolicy.Rule = (
            _,
            signal
        ) => signal.ResourceId == admitted;

        await using var stream = await SignalProbe.OpenAsync(_client);

        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: Guid.NewGuid(),
                Label: "refusée"
            )
        );
        await PublishAsync(new ProbeIntegrationEvent(
                ThingId: admitted,
                Label: "admise"
            )
        );

        // Act

        await ProcessPendingAsync();
        await SignalProbe.TailAsync(Factory);

        // Assert

        (await stream.ReadSignalAsync(Timeout)).GetProperty("resourceId").GetGuid().Should().Be(admitted);
    }

    [Test]
    public async Task Subscribe_ShouldReturn422WithTheSocleCode_WhenAResourceIsMalformed()
    {
        // Act

        var response = await _client.GetAsync("api/signals?resource=nope");

        // Assert — la forme d'erreur du socle, comme un IBAN invalide.

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType!.ToString().Should().Be($"{ApiProblem.ContentType}; charset=utf-8");

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        problem.GetProperty("code").GetString().Should().Be("INVALID_SIGNAL_RESOURCE");
        problem.GetProperty("parameters").GetProperty("resource").GetString().Should().Be("nope");
    }

    private static Task ProcessPendingAsync() =>
        Factory.Services
            .GetRequiredService<OutboxProcessor>()
            .ProcessPendingAsync(CancellationToken.None);

    private static async Task PublishAsync(IIntegrationEvent integrationEvent)
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(
                integrationEvent: integrationEvent,
                cancellationToken: CancellationToken.None
            );
    }
}
