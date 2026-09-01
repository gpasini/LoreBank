using MediatR;

namespace LoreBank.SharedKernel.Application;

public interface ICreationCommand : IMutatingRequest, IRequest<Guid>;
