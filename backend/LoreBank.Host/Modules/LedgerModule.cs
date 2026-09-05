using Autofac.Core;
using LoreBank.Ledger.Infrastructure;
using LoreBank.Ledger.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.Host.Modules;

// L'identité (assemblies, nom, clé de connexion « LedgerDb ») est dérivée de
// LedgerDbContext par la base — ne reste que ce qui ne se dérive pas.
public sealed class LedgerModule : HostModule<LedgerDbContext>
{
    public override IModule AutofacModule => new LedgerInfrastructureModule();
}
