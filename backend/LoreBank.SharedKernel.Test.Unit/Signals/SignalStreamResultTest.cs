using System.Text;
using System.Threading.Channels;
using LoreBank.SharedKernel.Api.Signals;
using LoreBank.SharedKernel.Application.Signals;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Unit.Signals;

// Le fil SSE, écrit sur un HttpContext nu : chaque Signal est un event
// nommé par son discriminant, le keep-alive est un commentaire, et le
// départ du client termine le flux sans erreur.
[TestFixture]
[TestOf(typeof(SignalStreamResult))]
public sealed class SignalStreamResultTest
{
    private static readonly Signal Deposited = new(
        Discriminant: "bank.money-deposited",
        ResourceKind: "bank-account",
        ResourceId: Guid.Parse("0f7d2a4e-9c3b-4b1e-8a6d-2f1c3e4d5a6b"),
        OccurredAt: new DateTimeOffset(
            year: 2026,
            month: 9,
            day: 9,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero
        )
    );

    [Test]
    public async Task ExecuteResultAsync_ShouldWriteEachSignalAsAnSseEvent_WithTheSignalAsData()
    {
        // Arrange

        var signals = Channel.CreateUnbounded<Signal>();
        var (context, body) = Context();

        var result = new SignalStreamResult(
            signals: signals.Reader.ReadAllAsync(),
            keepAlive: TimeSpan.FromMinutes(1)
        );

        // Act

        var executing = result.ExecuteResultAsync(context);

        await signals.Writer.WriteAsync(Deposited);
        signals.Writer.Complete();

        await executing.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert

        context.HttpContext.Response.StatusCode.Should().Be(200);
        context.HttpContext.Response.ContentType.Should().Be("text/event-stream; charset=utf-8");
        context.HttpContext.Response.Headers.CacheControl.ToString().Should().Be("no-cache");

        Text(body).Should().Be(
            ": keep-alive\n\n"
            + "data: {\"discriminant\":\"bank.money-deposited\",\"resourceKind\":\"bank-account\","
            + "\"resourceId\":\"0f7d2a4e-9c3b-4b1e-8a6d-2f1c3e4d5a6b\",\"occurredAt\":\"2026-09-09T12:00:00+00:00\"}\n"
            + "\n"
        );
    }

    [Test]
    public async Task ExecuteResultAsync_ShouldWriteAKeepAliveComment_AtStartAndWhenNothingPasses()
    {
        // Arrange — un flux qui ne dit rien, un keep-alive court, un client
        // qui part après en avoir vu passer. Le premier commentaire part à
        // l'ouverture : c'est lui qui met les en-têtes sur le fil.

        var signals = Channel.CreateUnbounded<Signal>();
        var (context, body) = Context();

        var result = new SignalStreamResult(
            signals: signals.Reader.ReadAllAsync(),
            keepAlive: TimeSpan.FromMilliseconds(20)
        );

        // Act

        var executing = result.ExecuteResultAsync(context);

        await Task.Delay(200);
        await Aborting(context).CancelAsync();

        await executing.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert — le départ du client est la fin normale, sans exception.

        Text(body).Should().StartWith(": keep-alive\n\n: keep-alive\n\n");
    }

    [Test]
    public async Task ExecuteResultAsync_ShouldEndQuietly_WhenTheClientLeavesMidStream()
    {
        // Arrange

        var signals = Channel.CreateUnbounded<Signal>();
        var (context, body) = Context();

        var result = new SignalStreamResult(
            signals: signals.Reader.ReadAllAsync(),
            keepAlive: TimeSpan.FromMinutes(1)
        );

        // Act

        var executing = result.ExecuteResultAsync(context);

        await signals.Writer.WriteAsync(Deposited);
        await Task.Delay(100);
        await Aborting(context).CancelAsync();

        var act = async () => await executing.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert

        await act.Should().NotThrowAsync();
        Text(body).Should().Contain("data: {\"discriminant\":\"bank.money-deposited\"");
    }

    private static (ActionContext Context, MemoryStream Body) Context()
    {
        var aborting = new CancellationTokenSource();
        var body = new MemoryStream();
        var httpContext = new DefaultHttpContext {
            RequestAborted = aborting.Token,
            RequestServices = new ServiceCollection().AddOptions().BuildServiceProvider(),
        };

        httpContext.Response.Body = body;
        httpContext.Items["aborting"] = aborting;

        return (new ActionContext(
            httpContext: httpContext,
            routeData: new RouteData(),
            actionDescriptor: new ActionDescriptor()
        ), body);
    }

    private static CancellationTokenSource Aborting(ActionContext context) =>
        (CancellationTokenSource)context.HttpContext.Items["aborting"]!;

    private static string Text(MemoryStream body) => Encoding.UTF8.GetString(body.ToArray());
}
