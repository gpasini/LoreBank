using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Les migrations ne tournent qu'en Development (Program.cs) : toute la suite
// d'intégration en dépend. Sans épingle, l'environnement de l'hôte de test
// hériterait de la variable ambiante ASPNETCORE_ENVIRONMENT du shell qui
// lance les tests — un runner CI exportant Production rendrait toute la suite
// rouge avec « relation does not exist », loin de la cause.
[TestFixture]
[TestOf(typeof(IntegrationTestWebAppFactory))]
public sealed class TestHostEnvironmentTest
{
    [Test]
    public void ConfigureWebHost_ShouldPinTheEnvironmentToDevelopment_WhateverTheShellExports()
    {
        TestHost<SharedKernelWebAppFactory>.Factory.Services
            .GetRequiredService<IWebHostEnvironment>()
            .EnvironmentName
            .Should()
            .Be(Environments.Development);
    }
}
