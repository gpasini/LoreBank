using System.Net;
using System.Text.Json;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// Vivacité et Disponibilité (ADR 0022), prouvées sur l'hôte du harnais :
// le module rapporté est le ProbeModule, jamais un module d'exemple
// (ADR 0017). Le cas « base injoignable » se prouve sur le check en
// isolation (ModuleDatabasesHealthCheckTest), pas ici.
[TestFixture]
public sealed class HealthContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private HttpClient _client = null!;

    [SetUp]
    public void SetUp() => _client = Factory.CreateClient();

    [TearDown]
    public void TearDown() => _client.Dispose();

    [Test]
    public async Task GetLive_ShouldReturn200_WhenTheProcessServes()
    {
        var response = await _client.GetAsync("health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetReady_ShouldReturn200AndListEachModule_WhenEveryDatabaseAnswers()
    {
        // Act

        var response = await _client.GetAsync("health/ready");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        body.GetProperty("status").GetString().Should().Be("Healthy");
        body.GetProperty("modules").GetProperty("Probe").GetString().Should().Be("Healthy");
    }
}
