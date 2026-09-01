using Autofac;
using LoreBank.Bank.Domain.Services;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

public sealed class IntegrationTestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("lorebank_tests")
        .WithUsername("lorebank")
        .WithPassword("lorebank")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
            {
                var descriptor = services.SingleOrDefault(s => s.ServiceType == typeof(DbContextOptions<BankDbContext>));

                if (descriptor is not null) {
                    services.Remove(descriptor);
                }

                services.AddDbContext<BankDbContext>(options => options.UseNpgsql(_dbContainer.GetConnectionString()));

                services.AddSingleton<ConfigurableWelcomeLetterSender>();
            }
        );
    }

    // La substitution d'`IWelcomeLetterSender` par le fake ne peut pas se faire dans
    // `ConfigureTestServices` : celui-ci peuple l'`IServiceCollection` avant que le
    // `ConfigureContainer` de l'hôte ne le transpose dans le conteneur Autofac et
    // n'exécute `BankInfrastructureModule` par-dessus. La dernière inscription
    // Autofac gagne, donc `LoggingWelcomeLetterSender` l'emporterait toujours sur
    // une inscription faite côté `IServiceCollection`. On surcharge donc ici, dans
    // un second `ConfigureContainer` ajouté après celui de l'hôte, pour que le fake
    // gagne à son tour.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureContainer<ContainerBuilder>(containerBuilder => {
                containerBuilder
                    .Register(context => context.Resolve<ConfigurableWelcomeLetterSender>())
                    .As<IWelcomeLetterSender>()
                    .SingleInstance();
            }
        );

        return base.CreateHost(builder);
    }

    public async Task StartAsync()
    {
        await _dbContainer.StartAsync();
    }

    public async Task StopAsync()
    {
        await _dbContainer.StopAsync();
    }
}
