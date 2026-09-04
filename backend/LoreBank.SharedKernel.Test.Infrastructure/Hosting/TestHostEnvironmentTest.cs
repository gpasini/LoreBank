using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// L'API ne migre plus à son démarrage (ADR 0006), mais l'environnement pilote
// toujours le chargement de la configuration (appsettings.Development.json).
// Sans épingle, l'environnement de l'hôte de test hériterait de la variable
// ambiante ASPNETCORE_ENVIRONMENT du shell qui lance les tests — et la config
// de l'hôte varierait d'un poste ou d'un runner CI à l'autre.
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
