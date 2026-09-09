using Autofac;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Events;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;

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
    }
}
