using MediatR;

namespace LoreBank.SharedKernel.Application;

public interface ICommand : IMutatingRequest, IRequest;
