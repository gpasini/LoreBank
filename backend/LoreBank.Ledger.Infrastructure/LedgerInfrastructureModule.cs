using Autofac;
using LoreBank.Ledger.Application.Readers;
using LoreBank.Ledger.Domain.Repositories;
using LoreBank.Ledger.Infrastructure.Readers;
using LoreBank.Ledger.Infrastructure.Repositories;

namespace LoreBank.Ledger.Infrastructure;

public sealed class LedgerInfrastructureModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder
            .RegisterType<JournalEntryRepository>()
            .As<IJournalEntryRepository>()
            .InstancePerLifetimeScope();

        builder
            .RegisterType<LedgerMovementReader>()
            .As<ILedgerMovementReader>()
            .InstancePerLifetimeScope();
    }
}
