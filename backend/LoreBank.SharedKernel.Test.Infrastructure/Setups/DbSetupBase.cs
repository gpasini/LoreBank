using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// Les données de test se créent via les vrais use cases, en scénario différé
// (ADR 0030) : le DbSetup d'un module dérive cette base et ajoute une classe
// partielle par agrégat, dont chaque geste (`CreateXxx`, `Deposit`…) prend
// un builder de l'entrée du module, empile une étape et renvoie le setup —
// rien ne s'exécute avant `RunAsync()`, qui rejoue les étapes dans l'ordre.
// C'est ce qui rend le chaînage possible sans bloquer sous le
// TransactionScope ambiant : un geste n'attend rien, seul le terminal est
// attendu — et un `RunAsync()` oublié est un CS4014, donc une erreur de build.
//
// Un accesseur (`GetLastXxxId()`, `GetXxxAsync()`) passe par `Arranged` : il
// lève tant qu'une étape attend, pour qu'une lecture avant le `await` ne
// rende pas un état incomplet en silence.
public abstract class DbSetupBase(IServiceProvider serviceProvider)
{
    private readonly List<Step> _pending = [];

    protected ISender Sender => serviceProvider.GetRequiredService<ISender>();

    public bool HasPendingSteps => _pending.Count > 0;

    protected T GetService<T>() where T : notnull => serviceProvider.GetRequiredService<T>();

    // Un geste empile son étape sous son nom — celui que l'échec citera. Le
    // builder se configure et le prérequis se comble à l'exécution, pas à
    // l'enregistrement : « le dernier créé » est celui de l'étape précédente.
    protected void Enqueue(
        string name,
        Func<Task> step
    ) => _pending.Add(
        new Step(
            Name: name,
            Run: step
        )
    );

    protected T Arranged<T>(Func<T> read)
    {
        if (HasPendingSteps) {
            throw new InvalidOperationException(
                $"{_pending.Count} étape(s) du DbSetup en attente ({string.Join(
                    separator: ", ",
                    values: _pending.Select(step => step.Name)
                )}) : appeler RunAsync() avant de lire l'état arrangé."
            );
        }

        return read();
    }

    // Rejoue les étapes dans l'ordre et vide la file — le setup est
    // réutilisable pour un second scénario dans le même test (un Instant
    // différent, par exemple). Un échec est enveloppé avec le rang et le nom
    // de l'étape : un arrange qui rate se distingue d'un act qui rate au
    // premier coup d'œil, et l'inner garde le type et le code de l'erreur.
    public async Task RunAsync()
    {
        var steps = _pending.ToList();

        _pending.Clear();

        for (var index = 0; index < steps.Count; index++) {
            try {
                await steps[index].Run();
            } catch (Exception exception) {
                throw new InvalidOperationException(
                    message: $"Étape {index + 1} ({steps[index].Name}) du DbSetup a échoué : {exception.Message}",
                    innerException: exception
                );
            }
        }
    }

    private sealed record Step(
        string Name,
        Func<Task> Run
    );
}
