using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le pendant test de HostModules.All : ces assertions rendent rouges les oublis
// de montage qui, sans elles, ne se verraient qu'au premier appel HTTP (handler
// MediatR absent) ou à la première requête SQL (migration manquante).
[TestFixture]
[TestOf(typeof(HostModules))]
public sealed class ModuleCompositionTest
{
    private static IEnumerable<IHostModule> Modules => HostModules.All;

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldResolveAHandlerForEveryRequest_WhenTheModuleIsDeclared(IHostModule module)
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        var requestTypes = module.ApplicationAssembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false } && type.IsAssignableTo(typeof(IBaseRequest)))
            .ToList();

        requestTypes.Should().NotBeEmpty();

        foreach (var requestType in requestTypes) {
            var responseType = requestType
                .GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>))
                ?.GetGenericArguments()
                .Single();

            var handlerType = responseType is null
                ? typeof(IRequestHandler<>).MakeGenericType(requestType)
                : typeof(IRequestHandler<,>).MakeGenericType([requestType, responseType]);

            scope.ServiceProvider
                .GetService(handlerType)
                .Should()
                .NotBeNull($"la requête {requestType.Name} doit résoudre un handler depuis le conteneur de l'hôte");
        }
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldMountEveryController_WhenTheModuleIsDeclared(IHostModule module)
    {
        var feature = new ControllerFeature();
        TestHost<SharedKernelWebAppFactory>.Factory.Services
            .GetRequiredService<ApplicationPartManager>()
            .PopulateFeature(feature);

        var moduleControllers = module.ControllerAssembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false } && type.IsAssignableTo(typeof(ControllerBase)))
            .ToList();

        moduleControllers.Should().NotBeEmpty();
        moduleControllers.Should().BeSubsetOf(feature.Controllers.Select(controller => controller.AsType()));
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldResolveEveryDomainEventHandler_WhenTheModuleIsDeclared(IHostModule module)
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        // Pas de garde NotBeEmpty : un module sans handler est légitime. Le
        // mécanisme de scan est unique et côté hôte — le module Bank, qui a un
        // handler, suffit à prouver qu'il fonctionne pour tous.
        var handlerInterfaces = module.DomainAssembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .SelectMany(type => type.GetInterfaces())
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            .Distinct()
            .ToList();

        foreach (var handlerInterface in handlerInterfaces) {
            scope.ServiceProvider
                .GetService(handlerInterface)
                .Should()
                .NotBeNull($"le handler {handlerInterface.GenericTypeArguments.Single().Name} doit être résolu par le scan de DomainAssembly côté hôte");
        }
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldDeriveEveryDbContextFromModuleDbContext_WhenTheModuleIsDeclared(IHostModule module)
    {
        var dbContextTypes = ModuleDbContexts.Of(module);

        dbContextTypes.Should().NotBeEmpty("un module a un DbContext par doctrine");

        foreach (var dbContextType in dbContextTypes) {
            dbContextType
                .Should()
                .BeAssignableTo<ModuleDbContext>($"le dispatch des domain events vit dans ModuleDbContext — un {dbContextType.Name} qui n'en dérive pas ne dispatcherait jamais rien");
        }
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldResolveADbContextWithoutPendingModelChanges_WhenTheModuleIsDeclared(IHostModule module)
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        var dbContextTypes = ModuleDbContexts.Of(module);

        dbContextTypes.Should().NotBeEmpty("un module a un DbContext par doctrine");

        foreach (var dbContextType in dbContextTypes) {
            var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(dbContextType);

            dbContext.Database
                .HasPendingModelChanges()
                .Should()
                .BeFalse($"le modèle de {dbContextType.Name} doit être couvert par une migration (dotnet-ef migrations add)");
        }
    }
}
