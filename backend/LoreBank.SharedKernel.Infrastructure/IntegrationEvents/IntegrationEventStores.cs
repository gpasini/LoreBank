using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

// L'accès aux tables d'integration events d'un module monté, et surtout :
// les deux politiques de scope, nommées. C'est la seule chose qu'un appelant
// a à choisir, et c'est une décision transactionnelle, pas une commodité —
//
//   InCallerScopeAsync : le DbContext du scope de l'appelant. La connexion
//   empruntée s'enrôle dans son TransactionScope ambiant, donc la ligne part
//   avec la transaction de la commande ou pas du tout. C'est toute la
//   promesse de l'outbox, et c'est la porte du publisher — qui publie, et
//   n'a rien à voir avec l'inbox.
//
//   InOwnScopeAsync : un scope neuf, donc une connexion distincte, hors de
//   toute transaction de handler — joindre celle du handler ferait escalader
//   en distribué (ADR 0014). C'est la porte du processor et du suiveur de
//   Signal : réserver, marquer, mesurer, purger, relire. Elle rend les deux
//   stores du module : ils vivent dans le même schéma et la purge les
//   balaie ensemble.
//
// Les stores eux-mêmes ne connaissent ni scope ni conteneur : ils se
// construisent sur un DbContext résolu, et se testent ainsi.
//
// L'inbox du chemin de livraison, elle, ne passe pas par ici : le processor
// possède déjà le scope où elle s'écrit — celui de son handler, avec sa
// transaction — et y résout le DbContext du module consommateur.
//
// Le type est public parce qu'il paraît dans les constructeurs de services
// publics que le conteneur résout ; sa surface, elle, est interne — hors de
// l'assembly et de son harnais, il n'y a rien à en faire.
public sealed class IntegrationEventStores(
    IServiceProvider services,
    IEnumerable<IHostModule> modules
)
{
    internal async Task InCallerScopeAsync(
        IServiceProvider callerScope,
        string moduleName,
        string purpose,
        Func<Outbox, Task> action
    )
    {
        var dbContext = ModuleDbContexts.Resolve(
            modules: modules,
            services: callerScope,
            moduleName: moduleName,
            purpose: purpose
        );

        await action(new Outbox(dbContext));
    }

    internal async Task<T> InOwnScopeAsync<T>(
        IHostModule module,
        Func<Outbox, Inbox, Task<T>> action
    )
    {
        await using var scope = services.CreateAsyncScope();

        var dbContext = ModuleDbContexts.Resolve(
            services: scope.ServiceProvider,
            module: module
        );

        return await action(
            arg1: new Outbox(dbContext),
            arg2: new Inbox(dbContext)
        );
    }

    internal Task InOwnScopeAsync(
        IHostModule module,
        Func<Outbox, Inbox, Task> action
    ) =>
        InOwnScopeAsync(
            module: module,
            action: async (
                outbox,
                inbox
            ) =>
            {
                await action(
                    arg1: outbox,
                    arg2: inbox
                );

                return 0;
            }
        );
}
