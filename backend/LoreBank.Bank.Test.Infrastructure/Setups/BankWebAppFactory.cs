using Autofac;
using LoreBank.Bank.Domain.Services;
using LoreBank.Bank.Test.Infrastructure.Fakes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

// L'hôte de test du module : le socle démarre l'hôte réel contre le
// Testcontainer, le module n'apporte que ses fakes.
public sealed class BankWebAppFactory : IntegrationTestWebAppFactory
{
    protected override void ConfigureModuleContainer(ContainerBuilder builder)
    {
        builder
            .RegisterType<ConfigurableWelcomeLetterSender>()
            .AsSelf()
            .As<IWelcomeLetterSender>()
            .SingleInstance();
    }

    public override void ResetFakes() =>
        Services.GetRequiredService<ConfigurableWelcomeLetterSender>().Reset();
}
