using System.Text.Json;
using LoreBank.SharedKernel.Api.Handlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace LoreBank.SharedKernel.Test.Unit.Handlers;

[TestFixture]
[TestOf(typeof(UnhandledExceptionHandler))]
public sealed class UnhandledExceptionHandlerTest
{
    [Test]
    public async Task TryHandleAsync_ShouldProduce500_WhenAnyExceptionReachesIt()
    {
        // Arrange

        var context = ContextWithABufferedBody();

        // Act

        var handled = await HandleAsync(context);

        // Assert

        handled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(500);
    }

    [Test]
    public async Task TryHandleAsync_ShouldUseTheProblemJsonContentType_WhenAnyExceptionReachesIt()
    {
        var context = ContextWithABufferedBody();

        await HandleAsync(context);

        context.Response.ContentType.Should().Be("application/problem+json; charset=utf-8");
    }

    [Test]
    public async Task TryHandleAsync_ShouldTitleWithTheStatusReasonPhrase_WhenAnyExceptionReachesIt()
    {
        var context = ContextWithABufferedBody();

        await HandleAsync(context);

        (await BodyOf(context)).GetProperty("title").GetString().Should().Be("Internal Server Error");
    }

    // Le message de l'exception ne doit pas fuir : c'est toute la différence
    // avec la page d'exception de développement qu'on a renoncé à monter.
    [Test]
    public async Task TryHandleAsync_ShouldExposeNothingButTheStatus_WhenAnyExceptionReachesIt()
    {
        var context = ContextWithABufferedBody();

        await HandleAsync(context);

        var body = await BodyOf(context);

        body.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo(
                "title",
                "status"
            );
    }

    private static Task<bool> HandleAsync(HttpContext context) =>
        new UnhandledExceptionHandler(NullLogger<UnhandledExceptionHandler>.Instance)
            .TryHandleAsync(
                httpContext: context,
                exception: new InvalidOperationException("boom"),
                cancellationToken: CancellationToken.None
            )
            .AsTask();

    private static DefaultHttpContext ContextWithABufferedBody() => new() {
        Response = { Body = new MemoryStream() },
    };

    private static async Task<JsonElement> BodyOf(HttpContext context)
    {
        context.Response.Body.Position = 0;

        var document = await JsonDocument.ParseAsync(context.Response.Body);

        return document.RootElement;
    }
}
