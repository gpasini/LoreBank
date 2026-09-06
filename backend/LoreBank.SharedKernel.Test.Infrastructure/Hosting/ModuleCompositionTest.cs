using System.Reflection;
using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Infrastructure.Modules;
using LoreBank.SharedKernel.Infrastructure.Persistence.DataMigrations;
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

    [Test]
    public void All_ShouldGiveEachModuleItsOwnSchema_WhenTheModulesAreDeclared()
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        // On lit le modèle EF effectif, pas la propriété Schema : une
        // régression de la base qui cesserait d'appliquer HasDefaultSchema
        // rougirait ici aussi.
        var schemas = HostModules.All
            .Select(module => ((DbContext)scope.ServiceProvider.GetRequiredService(module.DbContextType)).Model.GetDefaultSchema())
            .ToList();

        schemas.Should().OnlyContain(
            predicate: schema => !string.IsNullOrWhiteSpace(schema) && schema != "public",
            because: "un schéma PostgreSQL nommé par module, jamais public — la précondition du partage d'une même base par le harnais (ADR 0002)"
        );
        schemas.Should().OnlyHaveUniqueItems("deux modules qui partagent un schéma se marcheraient dessus dans la base commune des tests");
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldStampEveryDataMigration_WhenTheModuleIsDeclared(IHostModule module)
    {
        // IdOf lève si [DataMigration] manque — le test rougit avec le message
        // du socle plutôt qu'au premier `migrate` sur une vraie base. Le
        // timestamp est la position dans la timeline fusionnée : sa forme est
        // celle des ids EF, et un doublon rendrait l'ordre ambigu.
        var ids = DataMigrations
            .DiscoverIn(module.DbContextType.Assembly)
            .Select(DataMigrations.IdOf)
            .ToList();

        foreach (var id in ids) {
            id.Split('_')[0].Should().MatchRegex(
                regularExpression: "^[0-9]{14}$",
                because: $"le timestamp de {id} doit avoir la forme des ids EF (14 chiffres) pour se trier dans la même timeline"
            );
        }

        ids.Should().OnlyHaveUniqueItems("deux migrations de données au même id n'ont pas d'ordre défini dans la timeline");
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldKeepEveryDataMigrationInTheInfrastructureAssembly_WhenTheModuleIsDeclared(IHostModule module)
    {
        // Le pendant de la garde sur les IDomainEventHandler : une migration
        // de données n'est découverte que dans l'assembly du DbContext — une
        // classe rangée ailleurs échapperait au scan en silence.
        var misplacedMigrations = new[] {
                module.ControllerAssembly,
                module.ApplicationAssembly,
                module.DomainAssembly
            }
            .SelectMany(assembly => DataMigrations.DiscoverIn(assembly))
            .ToList();

        misplacedMigrations.Should().BeEmpty("une migration de données n'est scannée que dans l'assembly du DbContext — à déménager vers Persistence/DataMigrations/ de l'Infrastructure du module");
    }

    [TestCaseSource(nameof(Modules))]
    public async Task All_ShouldJournalEveryDataMigration_WhenTheModuleIsDeclared(IHostModule module)
    {
        // TestHost a migré par ModuleMigrator : chaque migration de données
        // découverte doit donc être passée par le runner et journalisée —
        // c'est la preuve de bout en bout que la découverte, la timeline et le
        // journal sont câblés, y compris sur base vide (no-op journalisé).
        await using var runner = DataMigrationRunner.Create(
            services: TestHost<SharedKernelWebAppFactory>.Factory.Services,
            dbContextType: module.DbContextType
        );

        var applied = await runner.GetAppliedIdsAsync(CancellationToken.None);

        foreach (var migrationType in DataMigrations.DiscoverIn(module.DbContextType.Assembly)) {
            applied.Should().Contain(
                expected: DataMigrations.IdOf(migrationType),
                because: $"{migrationType.Name} doit avoir été appliquée et journalisée par la migration du harnais"
            );
        }
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldKeepEveryIntegrationEventHandlerInScannedAssemblies_WhenTheModuleIsDeclared(IHostModule module)
    {
        // La jumelle de la garde sur les IDomainEventHandler : un
        // IIntegrationEventHandler<> n'est découvert que dans Domain et
        // Application (IntegrationEventHandlers.DiscoverIn). Rangé dans Api ou
        // Infrastructure, il échapperait au scan — et le mode d'échec est le
        // pire du socle : ses events seraient marqués « livrés à personne =
        // livrés », sans erreur ni retry.
        var misplacedHandlers = new[] {
                module.ControllerAssembly,
                module.DbContextType.Assembly
            }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Where(type => type
                .GetInterfaces()
                .Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IIntegrationEventHandler<>))
            )
            .ToList();

        misplacedHandlers.Should().BeEmpty("un IIntegrationEventHandler<> n'est scanné que dans Domain et Application — rangé ailleurs, ses events partiraient en « livré à personne » en silence");
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldResolveEveryIntegrationEventHandler_WhenTheModuleIsDeclared(IHostModule module)
    {
        using var scope = TestHost<SharedKernelWebAppFactory>.Factory.Services.CreateScope();

        // Un constructeur aux dépendances non résolubles ne se verrait qu'à la
        // première livraison — en backoff puis poison, jamais en rouge de
        // build. Pas de NotBeEmpty : un module qui ne consomme rien est
        // légitime, et la garde de rangement ci-dessus tient l'inventaire.
        foreach (var registration in IntegrationEventHandlers.DiscoverIn(module)) {
            var act = () => scope.ServiceProvider.GetRequiredService(registration.HandlerType);

            act.Should().NotThrow($"le handler {registration.HandlerType.Name} doit se résoudre avec toutes ses dépendances depuis le conteneur de l'hôte");
        }
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldOnlyReferenceContractsOfOtherModules_WhenTheModuleIsDeclared(IHostModule module)
    {
        // Le compilateur garde la frontière des références existantes (ADR
        // 0015), mais rien n'empêchait d'ajouter un <ProjectReference> vers
        // l'Application du voisin. Limite assumée : les références inutilisées
        // sont élaguées des métadonnées — le sweep ne voit un lien illégal
        // qu'au premier usage. C'est le bon moment : c'est l'usage qui abolit
        // la frontière, pas la ligne de csproj.
        var root = module.DbContextType.Assembly.GetName().Name!.Split('.')[0];
        var otherModules = HostModules.All
            .Where(other => other.ModuleName != module.ModuleName)
            .Select(other => other.ModuleName)
            .ToHashSet();

        var illegalReferences = new[] {
                module.ControllerAssembly,
                module.ApplicationAssembly,
                module.DomainAssembly,
                module.DbContextType.Assembly
            }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Select(reference => reference.Name!)
            .Where(name => name.Split('.') is [var referencedRoot, var segment, ..]
                && referencedRoot == root
                && otherModules.Contains(segment)
            )
            .Where(name => !name.EndsWith(
                value: ".Contracts",
                comparisonType: StringComparison.Ordinal
            ))
            .Distinct()
            .ToList();

        illegalReferences.Should().BeEmpty("les Contrats sont la seule surface d'un autre module qu'on a le droit de référencer (ADR 0015)");
    }

    [TestCaseSource(nameof(Modules))]
    public void All_ShouldKeepContractsFreeOfInternalReferences_WhenTheModulePublishes(IHostModule module)
    {
        var root = module.DbContextType.Assembly.GetName().Name!.Split('.')[0];

        Assembly contracts;

        try {
            contracts = Assembly.Load($"{root}.{module.ModuleName}.Contracts");
        }
        catch (FileNotFoundException) {
            // Un module qui ne publie rien n'a pas de projet Contracts — la
            // doctrine « 6, +1 si le module publie » rend l'absence légitime.
            return;
        }

        // « Primitives seulement, matériellement » (ADR 0015) : la seule
        // dépendance admise d'un Contracts est SharedKernel.Contracts — pas de
        // SharedKernel.Domain, donc pas de VO possible dans un integration
        // event ou un DTO publié.
        var illegalReferences = contracts
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name.StartsWith(
                value: $"{root}.",
                comparisonType: StringComparison.Ordinal
            ))
            .Where(name => name != $"{root}.SharedKernel.Contracts")
            .ToList();

        illegalReferences.Should().BeEmpty("un projet Contracts ne dépend que de SharedKernel.Contracts — toute autre référence ferait fuir des types internes dans le langage publié (ADR 0015)");
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
