using Autofac.Core;
using LoreBank.Bank.Infrastructure;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Modules;

namespace LoreBank.Host.Modules;

// L'identité (assemblies, nom, clé de connexion « BankDb ») est dérivée de
// BankDbContext par la base — ne reste que ce qui ne se dérive pas.
public sealed class BankModule : HostModule<BankDbContext>
{
    public override IModule AutofacModule => new BankInfrastructureModule();
}
