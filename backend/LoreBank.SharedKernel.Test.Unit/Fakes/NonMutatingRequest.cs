using MediatR;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed record NonMutatingRequest : IRequest<string>;
