using System.Reflection;
using NUnit.Framework.Internal;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Base des fixtures qui parlent à l'hôte réel SANS transaction rollbackée : les
// contrats HTTP (le TransactionScope ambiant ne traverse pas la frontière HTTP,
// une requête servie par l'hôte écrirait hors du rollback) et les tests qui
// observent un rollback réel (un scope interne non complété condamnerait
// l'ambiant). Fournit l'hôte partagé et le point unique de remise à zéro des
// fakes du module.
public abstract class BaseHostTest<TFactory> where TFactory : IntegrationTestWebAppFactory, new()
{
    protected static TFactory Factory => TestHost<TFactory>.Factory;

    [SetUp]
    public void HostSetUp()
    {
        AssertSerialExecutionIsDeclared();
        Factory.ResetFakes();
    }

    [TearDown]
    public void HostTearDown() => Factory.ResetFakes();

    // L'hypothèse d'exécution en série n'est pas héritable : l'attribut est par
    // assembly, et son oubli dans un nouveau module ne produirait qu'une suite
    // flaky en CI — on la vérifie ici, où l'oubli casse au premier test avec
    // un message qui dit quoi faire.
    private void AssertSerialExecutionIsDeclared()
    {
        var assembly = GetType().Assembly;
        var scope = assembly
            .GetCustomAttributes<ParallelizableAttribute>()
            .Select(attribute => attribute.Properties.Get(PropertyNames.ParallelScope))
            .FirstOrDefault();

        if (!Equals(
                objA: scope,
                objB: ParallelScope.None
            )) {
            throw new InvalidOperationException(
                $"L'assembly {assembly.GetName().Name} ne déclare pas [assembly: Parallelizable(ParallelScope.None)] : "
                + "les fakes de l'hôte de test sont des singletons mutables non synchronisés, une exécution "
                + "parallèle rendrait la suite flaky. Ajouter l'attribut dans le GlobalUsings.cs du projet."
            );
        }
    }
}
