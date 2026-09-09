using System.Text.Json;
using LoreBank.SharedKernel.Api.Handlers;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// La Corrélation (ADR 0022) relie les deux bouts : le traceId que le client
// reçoit dans son erreur est le TraceId que le hosting a posé en scope sur
// chaque log de la requête. Le log observé est celui du 500 — le seul cas
// où un exploitant part d'un identifiant cité par un client.
[TestFixture]
public sealed class CorrelationContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [Test]
    public async Task Get_ShouldLogUnderTheSameTraceId_ThatTheErrorResponseCarries()
    {
        // Arrange

        using var client = Factory.CreateClient();

        // Act

        var response = await client.GetAsync("api/probe/crash");

        // Assert

        var traceId = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("traceId").GetString();

        var entry = ProbeLogs.Entries
            .Should().ContainSingle(candidate => candidate.Category == typeof(UnhandledExceptionHandler).FullName)
            .Subject;

        entry.Scopes.Should().ContainKey("TraceId");
        entry.Scopes["TraceId"]!.ToString().Should().Be(traceId);
    }
}
