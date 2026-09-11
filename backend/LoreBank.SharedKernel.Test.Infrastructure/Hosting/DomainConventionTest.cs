using System.Reflection;
using LoreBank.Host.Modules;
using LoreBank.SharedKernel.Domain.Aggregates;
using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Domain.ValueObjects;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le pendant test des conventions du domaine : sans ces gardes, « constructeur
// privé », « VO immuable » et « sealed » resteraient des consignes qu'un module
// peut enfreindre sans qu'aucun test ne rougisse. Le sweep couvre les
// DomainAssembly de HostModules.All et SharedKernel.Domain, qui porte les VO
// transverses.
[TestFixture]
[TestOf(typeof(HostModules))]
public sealed class DomainConventionTest
{
    private static IEnumerable<Assembly> DomainAssemblies => HostModules.All
        .Select(module => module.DomainAssembly)
        .Append(typeof(ValueObject).Assembly)
        .Distinct();

    [TestCaseSource(nameof(DomainAssemblies))]
    public void All_ShouldKeepEveryAggregateConstructorPrivate_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // Un constructeur public court-circuite la factory de naissance : un
        // agrégat construit sans elle n'émet pas son event et n'a validé aucun
        // invariant initial.
        foreach (var aggregateType in TypesOf(assembly).Where(IsAggregate)) {
            aggregateType
                .GetConstructors()
                .Should()
                .BeEmpty($"{aggregateType.Name} doit naître par sa factory statique — constructeur privé, plus la concession EF sans paramètre");
        }
    }

    [TestCaseSource(nameof(DomainAssemblies))]
    public void All_ShouldKeepEveryValueObjectConstructorNonPublic_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // La création passe par une factory nommée qui valide et normalise ;
        // la réhydratation par Hydrate, qui truste la base (ADR 0016). Un
        // constructeur public rendrait le geste ambigu — et le prochain VO
        // cloné réintroduirait l'ancien monde en silence.
        foreach (var valueObjectType in TypesOf(assembly).Where(type => type.IsAssignableTo(typeof(ValueObject)))) {
            valueObjectType
                .GetConstructors()
                .Should()
                .BeEmpty($"{valueObjectType.Name} doit s'instancier par ses factories — création nommée qui valide, Hydrate qui truste la base");
        }
    }

    [TestCaseSource(nameof(DomainAssemblies))]
    public void All_ShouldKeepEveryValueObjectImmutable_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // Un setter — même private ou init — sur un VO casse la garantie
        // « invalide ne peut pas être créé » : toute la validation vit dans la
        // factory de création, une mutation la contournerait.
        foreach (var valueObjectType in TypesOf(assembly).Where(type => type.IsAssignableTo(typeof(ValueObject)))) {
            foreach (var property in valueObjectType.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
                property.SetMethod
                    .Should()
                    .BeNull($"{valueObjectType.Name}.{property.Name} doit être get-only — un VO ne mute jamais, une opération retourne une nouvelle instance");
            }
        }
    }

    [TestCaseSource(nameof(DomainAssemblies))]
    public void All_ShouldSealEveryDomainType_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // Les familles du domaine sont fermées : VO, agrégats, events et
        // exceptions se déclarent sealed — hériter d'un membre concret
        // fragiliserait son égalité (VO), son cycle de vie (agrégat) ou la
        // dérivation de son code (exception).
        var unsealedTypes = TypesOf(assembly)
            .Where(type => IsAggregate(type)
                || type.IsAssignableTo(typeof(ValueObject))
                || type.IsAssignableTo(typeof(IDomainEvent))
                || type.IsAssignableTo(typeof(DomainException))
            )
            .Where(type => !type.IsSealed)
            .ToList();

        unsealedTypes.Should().BeEmpty("les types concrets du domaine sont sealed ; une variante se modélise par un nouveau type, pas par héritage");
    }

    [TestCaseSource(nameof(DomainAssemblies))]
    public void All_ShouldReferenceNothingButTheSocle_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // Les erreurs métier sont des exceptions, pas un Result (ADR 0012,
        // docs/erreurs.md). La route la plus courte vers un Result est un
        // paquet — FluentResults, ErrorOr, OneOf — et un Domain qui en
        // référence un l'a déjà prise. Le Domain ne dépend que du runtime et
        // de la racine du repo : le socle et ses Contracts.
        var root = assembly.GetName().Name!.Split('.')[0];

        var foreignReferences = assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => name != "netstandard" && !name.StartsWith(
                value: "System.",
                comparisonType: StringComparison.Ordinal
            ))
            .Where(name => !name.StartsWith(
                value: $"{root}.",
                comparisonType: StringComparison.Ordinal
            ))
            .ToList();

        foreignReferences.Should().BeEmpty($"{assembly.GetName().Name} est un Domain : il ne référence aucun paquet — un Result y entrerait par là, et une erreur métier est une exception (ADR 0012)");
    }

    [TestCaseSource(nameof(DomainAssemblies))]
    public void All_ShouldNameNoTypeLikeAResult_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // L'autre route vers un Result est le type maison : Result<T>, Error,
        // Outcome, Either. Une heuristique de nom suffit à la voir — le
        // Domain n'a aucun usage légitime de ces mots, ses Result sont ceux
        // des queries, en Application (ADR 0012).
        var resultLikeTypes = assembly
            .GetTypes()
            .Where(type => !type.IsNested)
            .Where(type => ResultSuffixes.Any(suffix => type.Name.EndsWith(
                    value: suffix,
                    comparisonType: StringComparison.Ordinal
                ))
                || ResultPrefixes.Any(prefix => type.Name.StartsWith(
                    value: prefix,
                    comparisonType: StringComparison.Ordinal
                ))
            )
            .Select(type => type.Name)
            .ToList();

        resultLikeTypes.Should().BeEmpty("une erreur métier est une exception qui hérite de DomainException, jamais une valeur rendue (ADR 0012, docs/erreurs.md)");
    }

    private readonly static string[] ResultSuffixes = ["Result", "Error", "Outcome"];

    private readonly static string[] ResultPrefixes = ["Either", "Maybe", "OneOf"];

    private static IEnumerable<Type> TypesOf(Assembly assembly) => assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false, IsInterface: false });

    private static bool IsAggregate(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType) {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AggregateRoot<>)) {
                return true;
            }
        }

        return false;
    }
}
