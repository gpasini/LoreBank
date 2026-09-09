using System.Xml.Linq;
using LoreBank.Host;
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
        var referencing = Directory
            .EnumerateFiles(
                path: BackendDirectory(),
                searchPattern: "*.csproj",
                searchOption: SearchOption.AllDirectories
            )
            .Where(csproj => XDocument.Load(csproj)
                .Descendants("PackageReference")
                .Any(reference => reference.Attribute("Include")?.Value.StartsWith("OpenTelemetry") == true)
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

    // Le harnais tourne depuis bin/ : la racine du backend est le dossier
    // qui porte la solution.
    private static string BackendDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(
                   path1: directory.FullName,
                   path2: "LoreBank.slnx"
               ))) {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new InvalidOperationException("LoreBank.slnx introuvable au-dessus du dossier de test.");
    }
}
