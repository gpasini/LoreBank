using Autofac;
using LoreBank.Bank.Application.Readers;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.Bank.Domain.Services;
using LoreBank.Bank.Infrastructure.Readers;
using LoreBank.Bank.Infrastructure.Repositories;
using LoreBank.Bank.Infrastructure.Services;
using LoreBank.SharedKernel.Domain.Events;

namespace LoreBank.Bank.Infrastructure;

public sealed class BankInfrastructureModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder
            .RegisterType<BankAccountRepository>()
            .As<IBankAccountRepository>()
            .InstancePerLifetimeScope();

        builder
            .RegisterType<BankAccountReader>()
            .As<IBankAccountReader>()
            .InstancePerLifetimeScope();

        builder
            .RegisterAssemblyTypes(typeof(BankAccount).Assembly)
            .AsClosedTypesOf(typeof(IDomainEventHandler<>))
            .InstancePerLifetimeScope();

        builder
            .RegisterType<LoggingWelcomeLetterSender>()
            .As<IWelcomeLetterSender>()
            .InstancePerLifetimeScope();
    }
}
