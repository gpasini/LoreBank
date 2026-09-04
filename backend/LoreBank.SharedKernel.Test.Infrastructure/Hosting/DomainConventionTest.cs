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
    public void All_ShouldKeepEveryValueObjectImmutable_WhenTheAssemblyIsADomainAssembly(Assembly assembly)
    {
        // Un setter — même private ou init — sur un VO casse la garantie
        // « invalide ne peut pas exister » : toute la validation vit dans le
        // constructeur, une mutation la contournerait.
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
