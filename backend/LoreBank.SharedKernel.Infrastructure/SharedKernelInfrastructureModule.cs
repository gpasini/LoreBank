using Autofac;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Events;
using LoreBank.SharedKernel.Application.Signals;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Signals;

namespace LoreBank.SharedKernel.Infrastructure;

public sealed class SharedKernelInfrastructureModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder
            .RegisterType<DomainEventDispatcher>()
            .As<IDomainEventDispatcher>()
            .InstancePerLifetimeScope();

        // Par scope, comme le DbContext dont il emprunte la connexion : le
        // publisher écrit l'outbox dans la transaction de la commande courante.
        builder
            .RegisterType<OutboxPublisher>()
            .As<IIntegrationEventPublisher>()
            .InstancePerLifetimeScope();

        // Un Meter par hôte : les jauges d'outbox (ADR 0022), rafraîchies par
        // le processor, lues par tout exporteur ou par dotnet-counters.
        builder
            .RegisterType<OutboxMetrics>()
            .AsSelf()
            .SingleInstance();

        // Les Signaux (ADR 0026) : un hub par hôte — les abonnés de cette
        // instance — nourri par un suiveur par hôte, un curseur par module ;
        // la policy du socle laisse tout passer, le cloneur enregistre la
        // sienne par-dessus dans l'hôte.
        builder
            .RegisterType<SignalMetrics>()
            .AsSelf()
            .SingleInstance();
        builder
            .RegisterType<AllowAllSignalPolicy>()
            .As<ISignalPolicy>()
            .SingleInstance();
        builder
            .RegisterType<SignalHub>()
            .AsSelf()
            .As<ISignalStream>()
            .SingleInstance();
        builder
            .RegisterType<SignalTailer>()
            .AsSelf()
            .SingleInstance();
    }
}
