using System.Net;
using System.Text.Json;
using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Api.OpenApi;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le pendant Description de ModuleCompositionTest : chaque module de
// HostModules.All est scanné pour ses codes d'erreur — Domain et Application,
// les NotFoundException vivent dans la seconde. L'enum ErrorCode du document
// servi est exactement ce que le scan produit sur la liste de l'hôte : un
// module monté dont les codes manqueraient rendrait le Client incomplet en
// silence.
[TestFixture]
public sealed class ErrorCodesDescriptionTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [Test]
    public async Task ErrorCode_ShouldEnumerateTheCodesOfEveryMountedModule_WhenTheDocumentIsServed()
    {
        // Arrange

        var expected = ErrorCodes.DiscoverIn(HostModules.All.SelectMany(module => new[] {
                    module.DomainAssembly,
                    module.ApplicationAssembly,
                }
            )
        );

        using var client = Factory.CreateClient();

        // Act

        using var response = await client.GetAsync("openapi/v1.json");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var codes = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty(ErrorCodes.SchemaName)
            .GetProperty("enum").EnumerateArray().Select(code => code.GetString());

        codes.Should().Equal(expected);
        expected.Should().Contain(code => code.EndsWith("_NOT_FOUND"));
    }
}
