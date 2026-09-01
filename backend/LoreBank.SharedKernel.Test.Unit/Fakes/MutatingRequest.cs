using LoreBank.SharedKernel.Application;
using MediatR;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed record MutatingRequest : IMutatingRequest, IRequest<string>;
