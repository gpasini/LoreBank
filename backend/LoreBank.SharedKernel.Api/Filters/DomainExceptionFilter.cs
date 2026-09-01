using LoreBank.SharedKernel.Api.Problems;
using LoreBank.SharedKernel.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LoreBank.SharedKernel.Api.Filters;

public sealed class DomainExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not DomainException domainException) {
            return;
        }

        var status = domainException is NotFoundException
            ? StatusCodes.Status404NotFound
            : StatusCodes.Status422UnprocessableEntity;

        var problemDetails = ApiProblem.Create(
            status: status,
            code: domainException.Code,
            parameters: domainException.Parameters
        );

        context.Result = ApiProblem.ResultFor(problemDetails);
        context.ExceptionHandled = true;
    }
}
