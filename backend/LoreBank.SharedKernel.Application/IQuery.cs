using MediatR;

namespace LoreBank.SharedKernel.Application;

public interface IQuery<out TResponse> : IRequest<TResponse>;
