using System.Xml.Linq;
using LoreBank.Host;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.Configuration;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// La Télémétrie (ADR 0025) vit dans l'hôte, et l'hôte seul : le socle et
// les modules posent Activity et Meter en BCL, sans fournisseur. Ce test
// épingle la frontière sur les csproj du repo — une référence OpenTelemetry
// glissée dans un module ou dans le socle ne casserait rien d'autre — et
// l'interrupteur : l'exporteur n'est monté que si l'endpoint OTLP standard
// est défini, sinon rien ne sort, rien n'est tenté.
[TestFixture]
[TestOf(typeof(Telemetry))]
public sealed class TelemetryCompositionTest
{
    [Test]
    public void OnlyTheHostAndThisHarness_ShouldReferenceOpenTelemetry()
    {
        var referencing = SourceTree
            .BackendSources("*.csproj")
            .Where(csproj => XDocument.Load(csproj)
                .Descendants("PackageReference")
                .Any(reference => reference.Attribute("Include")?.Value.StartsWith(value: "OpenTelemetry", comparisonType: StringComparison.Ordinal) == true)
            )
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        referencing.Should().BeEquivalentTo([
                "LoreBank.Host",
                "LoreBank.SharedKernel.Test.Infrastructure",
            ]
        );
    }

    [Test]
    public void ExportsTo_ShouldBeFalse_WhenNoEndpointIsConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();

        Telemetry.ExportsTo(configuration).Should().BeFalse();
    }

    [Test]
    public void ExportsTo_ShouldBeFalse_WhenTheEndpointIsBlank()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Telemetry.EndpointKey] = " " })
            .Build();

        Telemetry.ExportsTo(configuration).Should().BeFalse();
    }

    [Test]
    public void ExportsTo_ShouldBeTrue_WhenTheStandardEndpointKeyIsSet()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> {
                [Telemetry.EndpointKey] = "http://localhost:4318",
            }
            )
            .Build();

        Telemetry.ExportsTo(configuration).Should().BeTrue();
    }
}
