using System.Reflection;
using System.Runtime.ExceptionServices;
using LoreBank.SharedKernel.Domain.Events;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.Events;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyList<IDomainEvent> domainEvents,
        CancellationToken cancellationToken
    )
    {
        foreach (var domainEvent in domainEvents) {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

            foreach (var handler in serviceProvider.GetServices(handlerType)) {
                await InvokeAsync(
                    handleMethod: handleMethod,
                    handler: handler!,
                    domainEvent: domainEvent,
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    // MethodInfo.Invoke emballe dans une TargetInvocationException ce qu'un handler
    // lève avant son premier await. On rend son exception d'origine, pile incluse.
    //
    // Un throw après le premier await est un chemin différent : il fait échouer
    // (fault) la Task retournée, et await la relève directement — ce catch ne le
    // voit jamais, aucune conversion n'est nécessaire dans ce cas.
    private static async Task InvokeAsync(
        MethodInfo handleMethod,
        object handler,
        IDomainEvent domainEvent,
        CancellationToken cancellationToken
    )
    {
        try {
            await (Task) handleMethod.Invoke(
                obj: handler,
                parameters: [domainEvent, cancellationToken]
            )!;
        } catch (TargetInvocationException exception) when (exception.InnerException is not null) {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}
