using Autofac.Core;
using LoreBank.Bank.Infrastructure;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Host.Modules;

public sealed class BankModule : HostModule<BankDbContext>
{
    public override IModule AutofacModule => new BankInfrastructureModule();

    public override void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<BankDbContext>(
            options => options.UseNpgsql(configuration.GetConnectionString("BankDb"))
        );
    }
}
