using System.Transactions;
using MediatR;

namespace LoreBank.SharedKernel.Application.Behaviors;

// Le scope est ambiant : le behavior ne connaît aucun DbContext, ce sont les
// connexions ouvertes à l'intérieur qui s'y enrôlent d'elles-mêmes.
//
// Pas de contrainte générique `where TRequest : IMutatingRequest` : le behavior
// est enregistré comme open behavior pour toute requête, et se neutralise
// lui-même au runtime pour ce qui n'est pas un `IMutatingRequest`.
// TransactionBehaviorTest vérifie qu'une query ne laisse aucune transaction
// ambiante derrière elle.
public sealed class TransactionBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        if (request is not IMutatingRequest) {
            return await next();
        }

        using var scope = new TransactionScope(
            scopeOption: TransactionScopeOption.Required,
            transactionOptions: new TransactionOptions {
                IsolationLevel = IsolationLevel.ReadCommitted,
                // Valeur par défaut de TransactionManager, écrite explicitement pour
                // que la limite ne reste pas implicite : une commande qui la dépasse
                // échoue avec une TransactionAbortedException.
                Timeout = TransactionManager.DefaultTimeout,
            },
            asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
        );

        var response = await next();

        scope.Complete();

        return response;
    }
}
