using LoreBank.FakeModule.Domain.Exceptions;
using LoreBank.SharedKernel.Api.Filters;
using LoreBank.SharedKernel.Test.Unit.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace LoreBank.SharedKernel.Test.Unit.Filters;

[TestFixture]
[TestOf(typeof(DomainExceptionFilter))]
public sealed class DomainExceptionFilterTest
{
    [Test]
    public void OnException_ShouldProduce422_WhenExceptionIsADomainException()
    {
        // Arrange

        var context = ContextFor(new ModuleFailureException("iban"));

        // Act

        new DomainExceptionFilter().OnException(context);

        // Assert

        ProblemDetailsOf(context).Status.Should().Be(422);
    }

    [Test]
    public void OnException_ShouldProduce404_WhenExceptionIsANotFoundException()
    {
        var context = ContextFor(new MissingThingException(Guid.Empty));

        new DomainExceptionFilter().OnException(context);

        ProblemDetailsOf(context).Status.Should().Be(404);
    }

    [Test]
    public void OnException_ShouldExposeTheCode_WhenExceptionIsADomainException()
    {
        var context = ContextFor(new ModuleFailureException("iban"));

        new DomainExceptionFilter().OnException(context);

        ProblemDetailsOf(context).Extensions["code"].Should().Be("FAKE_MODULE.MODULE_FAILURE");
    }

    [Test]
    public void OnException_ShouldExposeTheParameters_WhenExceptionIsADomainException()
    {
        var context = ContextFor(
            new TransverseFailureException(
                label: "iban",
                amount: 20.50m
            )
        );

        new DomainExceptionFilter().OnException(context);

        ProblemDetailsOf(context).Extensions["parameters"].Should().BeEquivalentTo(
            new Dictionary<string, object> {
                ["label"] = "iban",
                ["amount"] = 20.50m,
            }
        );
    }

    [Test]
    public void OnException_ShouldExposeEmptyParameters_WhenExceptionCarriesNoParameter()
    {
        var context = ContextFor(new ParameterlessFailureException());

        new DomainExceptionFilter().OnException(context);

        ProblemDetailsOf(context).Extensions["parameters"].Should().BeEquivalentTo(
            new Dictionary<string, object>()
        );
    }

    [Test]
    public void OnException_ShouldTitleWithTheStatusReasonPhrase_WhenExceptionIsADomainException()
    {
        var context = ContextFor(new ModuleFailureException("iban"));

        new DomainExceptionFilter().OnException(context);

        ProblemDetailsOf(context).Title.Should().Be("Unprocessable Entity");
    }

    [Test]
    public void OnException_ShouldLeaveDetailEmpty_WhenExceptionIsADomainException()
    {
        var context = ContextFor(new ModuleFailureException("iban"));

        new DomainExceptionFilter().OnException(context);

        ProblemDetailsOf(context).Detail.Should().BeNull();
    }

    [Test]
    public void OnException_ShouldUseTheProblemJsonContentType_WhenExceptionIsADomainException()
    {
        var context = ContextFor(new ModuleFailureException("iban"));

        new DomainExceptionFilter().OnException(context);

        context.Result.Should().BeOfType<ObjectResult>()
            .Subject.ContentTypes.Should().Equal("application/problem+json");
    }

    [Test]
    public void OnException_ShouldDoNothing_WhenExceptionIsNotADomainException()
    {
        var context = ContextFor(new InvalidOperationException("boom"));

        new DomainExceptionFilter().OnException(context);

        context.ExceptionHandled.Should().BeFalse();
        context.Result.Should().BeNull();
    }

    private static ExceptionContext ContextFor(Exception exception) => new(
        actionContext: new ActionContext(
            httpContext: new DefaultHttpContext(),
            routeData: new RouteData(),
            actionDescriptor: new ActionDescriptor()
        ),
        filters: []
    ) {
        Exception = exception,
    };

    private static ProblemDetails ProblemDetailsOf(ExceptionContext context) => context.Result
        .Should()
        .BeOfType<ObjectResult>()
        .Subject
        .Value
        .Should()
        .BeOfType<ProblemDetails>()
        .Subject;
}
