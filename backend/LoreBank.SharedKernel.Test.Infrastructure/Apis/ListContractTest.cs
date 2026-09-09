using System.Net;
using System.Text.Json;
using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Application;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Apis;

// La forme de référence d'une Liste (ADR 0027), prouvée sur le terrain du
// socle : la sonde de Liste du ProbeController — query liée sur la query
// string, reader-sonde déclaratif, moteur du socle. Bank ne prouve que
// l'emprunt du chemin (ListBankAccountsTest, CqsContractTest).
//
// Les lignes-terrain s'écrivent pour de vrai (BaseHostTest) et la table est
// partagée par d'autres sondes : chaque test marque ses libellés d'un jeton
// unique et cherche dessus — la recherche est le premier invariant prouvé,
// les autres se lisent sur un ensemble exact.
[TestFixture]
public sealed class ListContractTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private HttpClient _client = null!;

    private string _token = null!;

    [SetUp]
    public void SetUp()
    {
        _client = Factory.CreateClient();
        _token = Guid.NewGuid().ToString("N")[..8];
    }

    [TearDown]
    public void TearDown()
    {
        _client.Dispose();
    }

    [Test]
    public async Task Get_ShouldServeTheFirstPageWithTheDefaults_WhenOnlyTheSearchIsGiven()
    {
        // Arrange

        await ArrangeAsync(
            ("b", "one", true),
            ("a", "one", false),
            ("c", "two", true)
        );

        // Act

        var page = await ListAsync();

        // Assert

        page.EnumerateObject().Select(property => property.Name).Should().Equal(
            "items",
            "page",
            "pageSize",
            "totalCount",
            "facets"
        );
        page.GetProperty("page").GetInt32().Should().Be(1);
        page.GetProperty("pageSize").GetInt32().Should().Be(ListQuery<object>.DefaultPageSize);
        page.GetProperty("totalCount").GetInt32().Should().Be(3);
        Labels(page).Should().Equal(
            Label("a"),
            Label("b"),
            Label("c")
        );

        var item = page.GetProperty("items")[0];

        item.EnumerateObject().Select(property => property.Name).Should().BeEquivalentTo(
            "id",
            "label",
            "kind",
            "active"
        );
        item.GetProperty("kind").GetString().Should().Be("one");
        item.GetProperty("active").GetBoolean().Should().BeFalse();
    }

    [Test]
    public async Task Get_ShouldSliceByPage_AndServeAnEmptyPageBeyondTheLast()
    {
        // Arrange

        await ArrangeAsync(
            ("a", "one", true),
            ("b", "one", true),
            ("c", "one", true),
            ("d", "one", true),
            ("e", "one", true)
        );

        // Act

        var second = await ListAsync("page=2&pageSize=2");
        var beyond = await ListAsync("page=4&pageSize=2");

        // Assert

        Labels(second).Should().Equal(
            Label("c"),
            Label("d")
        );
        second.GetProperty("page").GetInt32().Should().Be(2);
        second.GetProperty("pageSize").GetInt32().Should().Be(2);
        second.GetProperty("totalCount").GetInt32().Should().Be(5);

        Labels(beyond).Should().BeEmpty();
        beyond.GetProperty("totalCount").GetInt32().Should().Be(5);
    }

    // Sous-chaîne, insensible à la casse, jokers littéraux : « 100% » cherche
    // un pour-cent, « a_b » un souligné — pas n'importe quel caractère.
    [Test]
    public async Task Get_ShouldSearchCaseInsensitively_WithLiteralWildcards()
    {
        // Arrange

        await ArrangeAsync(
            ("ALPHA 100%", "one", true),
            ("alpha beta", "one", true),
            ("a_b", "one", true),
            ("axb", "one", true)
        );

        // Act & Assert

        Labels(await ListAsync(search: "alpha")).Should().Equal(
            Label("ALPHA 100%"),
            Label("alpha beta")
        );
        Labels(await ListAsync(search: "100%")).Should().Equal(Label("ALPHA 100%"));
        Labels(await ListAsync(search: "a_b")).Should().Equal(Label("a_b"));
        Labels(await ListAsync(search: "nothing-like-it")).Should().BeEmpty();
    }

    // OU dans un filtre, ET entre filtres.
    [Test]
    public async Task Get_ShouldCombineFilters_OrWithinAFilterAndAcrossFilters()
    {
        // Arrange

        await ArrangeAsync(
            ("a", "one", true),
            ("b", "one", false),
            ("c", "two", true),
            ("d", "three", true)
        );

        // Act & Assert

        Labels(await ListAsync("kind=one&kind=two")).Should().Equal(
            Label("a"),
            Label("b"),
            Label("c")
        );
        Labels(await ListAsync("kind=one&active=true")).Should().Equal(Label("a"));
        Labels(await ListAsync("active=false&active=true")).Should().HaveCount(4);
    }

    // Une facette est comptée sur la recherche et les autres filtres, jamais le
    // sien : cocher « one » ne fait pas disparaître « two » de la facette kind,
    // mais restreint la facette active à ce que « one » contient. Seules les
    // valeurs présentes, comptes décroissants puis valeur, nom en camelCase,
    // booléen en minuscules.
    [Test]
    public async Task Get_ShouldCountFacetsWithoutTheirOwnFilter_OnPresentValuesOrderedByCountThenValue()
    {
        // Arrange

        await ArrangeAsync(
            ("a", "one", true),
            ("b", "one", false),
            ("c", "two", true),
            ("d", "three", true)
        );

        // Act

        var unfiltered = await ListAsync();
        var filtered = await ListAsync("kind=one");

        // Assert

        Facets(unfiltered).Should().Equal(
            ("kind", "one", 2),
            ("kind", "three", 1),
            ("kind", "two", 1),
            ("active", "true", 3),
            ("active", "false", 1)
        );
        Facets(filtered).Should().Equal(
            ("kind", "one", 2),
            ("kind", "three", 1),
            ("kind", "two", 1),
            ("active", "false", 1),
            ("active", "true", 1)
        );
    }

    [TestCase("page=0", 0, ListQuery<object>.DefaultPageSize)]
    [TestCase("pageSize=0", 1, 0)]
    [TestCase("pageSize=101", 1, 101)]
    public async Task Get_ShouldReturn422InvalidPaging_WhenOutOfTheBoundsOfTheSocle(
        string query,
        int page,
        int pageSize
    )
    {
        // Act

        var response = await _client.GetAsync($"api/probe/listings?{query}");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var body = await BodyOf(response);

        body.GetProperty("code").GetString().Should().Be("INVALID_PAGING");
        body.GetProperty("parameters").GetProperty("page").GetInt32().Should().Be(page);
        body.GetProperty("parameters").GetProperty("pageSize").GetInt32().Should().Be(pageSize);
        body.GetProperty("parameters").GetProperty("maxPageSize").GetInt32().Should().Be(ListQuery<object>.MaxPageSize);
    }

    // Un paramètre qui ne se lie pas est le 400 de binding, et le champ fautif
    // est nommé comme la Description le nomme : en camelCase.
    [Test]
    public async Task Get_ShouldReturn400ValidationFailed_NamingTheFieldAsTheDescriptionDoes_WhenAParameterDoesNotBind()
    {
        // Act

        var response = await _client.GetAsync("api/probe/listings?pageSize=beaucoup");

        // Assert

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await BodyOf(response);

        body.GetProperty("code").GetString().Should().Be("VALIDATION_FAILED");
        body.GetProperty("parameters").GetProperty("fields").EnumerateArray().Select(field => field.GetString())
            .Should().Equal("pageSize");
    }

    private async Task ArrangeAsync(params (string Label, string Kind, bool Active)[] things)
    {
        using var scope = Factory.Services.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<ProbeDbContext>();

        dbContext.ProbeThings.AddRange(things.Select(thing => new ProbeThing {
            Id = Guid.NewGuid(),
            Label = Label(thing.Label),
            Kind = thing.Kind,
            Active = thing.Active,
        }
            )
        );

        await dbContext.SaveChangesAsync();
    }

    private string Label(string label) => $"{_token} {label}";

    private async Task<JsonElement> ListAsync(
        string? query = null,
        string? search = null
    )
    {
        var response = await _client.GetAsync(
            $"api/probe/listings?search={Uri.EscapeDataString(search ?? _token)}{(query is null ? string.Empty : "&" + query)}"
        );

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        return await BodyOf(response);
    }

    private static List<string?> Labels(JsonElement page) => page.GetProperty("items").EnumerateArray()
        .Select(item => item.GetProperty("label").GetString())
        .ToList();

    private static List<(string Name, string? Value, int Count)> Facets(JsonElement page) => page.GetProperty("facets")
        .EnumerateArray()
        .SelectMany(facet => facet.GetProperty("values").EnumerateArray().Select(value => (
                    facet.GetProperty("name").GetString()!,
                    value.GetProperty("value").GetString(),
                    value.GetProperty("count").GetInt32()
                )
            )
        )
        .ToList();

    private static async Task<JsonElement> BodyOf(HttpResponseMessage response) => JsonDocument
        .Parse(await response.Content.ReadAsStringAsync())
        .RootElement;
}
