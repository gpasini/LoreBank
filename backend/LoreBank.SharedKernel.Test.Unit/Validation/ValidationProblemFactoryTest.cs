using LoreBank.SharedKernel.Api.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;

namespace LoreBank.SharedKernel.Test.Unit.Validation;

[TestFixture]
[TestOf(typeof(ValidationProblemFactory))]
public sealed class ValidationProblemFactoryTest
{
    // Le message que produit System.Text.Json quand le corps ne se lie pas : il
    // cite le type .NET visé, et c'est exactement ce qui ne doit pas sortir.
    private const string BindingMessage =
        "The JSON value could not be converted to LoreBank.Bank.Application.Commands.DepositMoney.DepositMoneyCommand.";

    [Test]
    public void Create_ShouldProduce400_WhenBindingFailed()
    {
        var result = ValidationProblemFactory.Create(ContextWithErrorsOn("$.amount"));

        ProblemDetailsOf(result).Status.Should().Be(400);
    }

    [Test]
    public void Create_ShouldTitleWithTheStatusReasonPhrase_WhenBindingFailed()
    {
        var result = ValidationProblemFactory.Create(ContextWithErrorsOn("$.amount"));

        ProblemDetailsOf(result).Title.Should().Be("Bad Request");
    }

    [Test]
    public void Create_ShouldExposeAStableCode_WhenBindingFailed()
    {
        var result = ValidationProblemFactory.Create(ContextWithErrorsOn("$.amount"));

        ProblemDetailsOf(result).Extensions["code"].Should().Be("VALIDATION_FAILED");
    }

    [Test]
    public void Create_ShouldStripTheJsonPathPrefix_WhenTheFaultyFieldIsInTheBody()
    {
        var result = ValidationProblemFactory.Create(
            ContextWithErrorsOn(
                "$.amount",
                "request"
            )
        );

        FieldsOf(result).Should().Equal(
            "amount",
            "request"
        );
    }

    [Test]
    public void Create_ShouldReportEachFieldOnce_WhenTheSameFieldFailsTwice()
    {
        var context = ContextWithErrorsOn("$.amount");
        context.ModelState.AddModelError(
            key: "$.amount",
            errorMessage: BindingMessage
        );

        FieldsOf(ValidationProblemFactory.Create(context)).Should().Equal("amount");
    }

    [Test]
    public void Create_ShouldLeakNeitherDetailNorBindingMessages_WhenBindingFailed()
    {
        var result = ValidationProblemFactory.Create(ContextWithErrorsOn("$.amount"));

        var problemDetails = ProblemDetailsOf(result);

        problemDetails.Detail.Should().BeNull();
        problemDetails.Extensions.Keys.Should().BeEquivalentTo(
            "code",
            "parameters"
        );
    }

    [Test]
    public void Create_ShouldUseTheProblemJsonContentType_WhenBindingFailed()
    {
        var result = ValidationProblemFactory.Create(ContextWithErrorsOn("$.amount"));

        result.Should().BeOfType<ObjectResult>()
            .Subject.ContentTypes.Should().Equal("application/problem+json");
    }

    private static ActionContext ContextWithErrorsOn(params string[] keys)
    {
        var context = new ActionContext(
            httpContext: new DefaultHttpContext(),
            routeData: new RouteData(),
            actionDescriptor: new ActionDescriptor()
        );

        foreach (var key in keys) {
            context.ModelState.AddModelError(
                key: key,
                errorMessage: BindingMessage
            );
        }

        return context;
    }

    private static string[] FieldsOf(IActionResult result) => ProblemDetailsOf(result)
        .Extensions["parameters"]
        .Should().BeAssignableTo<IReadOnlyDictionary<string, object>>()
        .Subject["fields"]
        .Should().BeOfType<string[]>()
        .Subject;

    private static ProblemDetails ProblemDetailsOf(IActionResult result) => result
        .Should()
        .BeOfType<ObjectResult>()
        .Subject
        .Value
        .Should()
        .BeOfType<ProblemDetails>()
        .Subject;
}
