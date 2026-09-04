using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.Modules;
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

        // Pas de garde NotBeEmpty : un module sans handler est légitime, et la
        // garde « aucun handler hors de DomainAssembly » ci-dessous garantit
        // qu'un handler existant ne peut pas être rangé ailleurs. Le mécanisme
        // de scan est unique et côté hôte — le module Bank, qui a un handler,
        // suffit à prouver qu'il fonctionne pour tous.
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
    public void All_ShouldKeepEveryDomainEventHandlerInTheDomainAssembly_WhenTheModuleIsDeclared(IHostModule module)
    {
        // Une DomainAssembly juste ne suffit pas : des handlers rangés dans
        // Application/ par réflexe Clean Architecture échapperaient au scan de
        // l'hôte sans que rien ne le signale — c'est cette garde qui rend
        // honnête l'absence de NotBeEmpty du test de résolution ci-dessus.
        var misplacedHandlers = new[] {
                module.ControllerAssembly,
                module.ApplicationAssembly,
                module.DbContextType.Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type
                .GetInterfaces()
                .Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>))
            )
            .ToList();

        misplacedHandlers.Should().BeEmpty("un IDomainEventHandler<> n'est scanné que dans DomainAssembly — à déménager vers Domain/EventHandlers/ du module");
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldNameTheModuleInEveryDomainExceptionNamespace_WhenTheModuleIsDeclared(IHostModule module)
    {
        // DomainException préfixe ses codes du 2e segment du namespace ; la
        // base HostModule dérive ModuleName de la même convention, côté nom
        // d'assembly. Si le segment correspond au nom, le préfixe est correct
        // par construction — sans re-dériver la conversion SCREAMING_SNAKE ici.
        var exceptionTypes = module.DomainAssembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false } && type.IsAssignableTo(typeof(DomainException)))
            .ToList();

        foreach (var exceptionType in exceptionTypes) {
            var segments = exceptionType.Namespace?.Split('.') ?? [];

            segments.Should().HaveCountGreaterThan(
                expected: 1,
                because: $"le namespace de {exceptionType.Name} doit suivre <Racine>.<Module>.<Couche>"
            );
            segments[1].Should().Be(
                expected: module.ModuleName,
                because: $"le code de {exceptionType.Name} est préfixé du 2e segment de son namespace, qui doit nommer le module"
            );
        }
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldResolveADbContextWithoutPendingModelChanges_WhenTheModuleIsDeclared(IHostModule module)
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        var dbContext = (DbContext)scope.ServiceProvider.GetRequiredService(module.DbContextType);

        dbContext.Database
            .HasPendingModelChanges()
            .Should()
            .BeFalse($"le modèle de {module.DbContextType.Name} doit être couvert par une migration (dotnet-ef migrations add)");
    }
}
