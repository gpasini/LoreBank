using System.Reflection;
using Autofac.Core;
using LoreBank.Bank.Api.Controllers;
using LoreBank.Bank.Application.Commands;
using LoreBank.Bank.Infrastructure;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.Modules;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.Host.Modules;

public sealed class BankModule : IHostModule
{
    public Assembly ControllerAssembly => typeof(BankAccountsController).Assembly;

    public Assembly ApplicationAssembly => typeof(OpenBankAccountCommand).Assembly;

    public IModule AutofacModule => new BankInfrastructureModule();

    public void ConfigureDbContext(
        IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddDbContext<BankDbContext>(
            options => options.UseNpgsql(configuration.GetConnectionString("BankDb"))
        );
    }

    public async Task MigrateAsync(IServiceProvider services)
    {
        await services.GetRequiredService<BankDbContext>().Database.MigrateAsync();
    }
}
