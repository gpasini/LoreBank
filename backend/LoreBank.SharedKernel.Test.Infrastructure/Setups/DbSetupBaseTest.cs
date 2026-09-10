namespace LoreBank.SharedKernel.Test.Infrastructure.Setups;

// La mécanique du scénario différé (ADR 0030), prouvée sans hôte ni base :
// un setup-sonde dont les gestes n'écrivent que dans une liste. Ce que les
// tests d'intégration d'un module ne prouvent pas, parce qu'ils font tous
// leur `await` : la garde des accesseurs, l'enveloppe d'échec, la file vidée.
[TestFixture]
[TestOf(typeof(DbSetupBase))]
public sealed class DbSetupBaseTest
{
    [Test]
    public async Task RunAsync_ShouldRunTheStepsInOrder()
    {
        var setup = new ProbeSetup();

        await setup.Create("a").Create("b").Create("c").RunAsync();

        setup.Created.Should().Equal(
            "a",
            "b",
            "c"
        );
    }

    // Le prérequis se comble à l'exécution : « le dernier créé » est celui
    // de l'étape précédente, pas celui qui existait quand le geste a été
    // enregistré.
    [Test]
    public async Task RunAsync_ShouldResolveThePrerequisiteAtRunTime()
    {
        var setup = new ProbeSetup();

        await setup.Create("a").Touch().Create("b").Touch().RunAsync();

        setup.Touched.Should().Equal(
            "a",
            "b"
        );
    }

    [Test]
    public async Task RunAsync_ShouldClearTheQueue_AndLeaveTheSetupReusable()
    {
        var setup = new ProbeSetup();

        await setup.Create("a").RunAsync();

        setup.HasPendingSteps.Should().BeFalse();

        await setup.Create("b").RunAsync();

        setup.Created.Should().Equal(
            "a",
            "b"
        );
    }

    [Test]
    public async Task RunAsync_ShouldDoNothing_WhenNoStepIsPending()
    {
        var setup = new ProbeSetup();

        await setup.RunAsync();

        setup.Created.Should().BeEmpty();
    }

    // L'échec cite le rang et le nom de l'étape et garde l'erreur d'origine
    // en inner ; les étapes suivantes ne s'exécutent pas, et la file est
    // vide — un scénario raté est terminé.
    [Test]
    public async Task RunAsync_ShouldWrapTheFailure_WithTheRankAndNameOfTheStep()
    {
        var setup = new ProbeSetup();

        var act = () => setup.Create("a").Fail("boom").Create("b").RunAsync();

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("Étape 2 (Fail) du DbSetup a échoué : boom")
            .WithInnerException<ArgumentException>()
            .WithMessage("boom");

        setup.Created.Should().Equal("a");
        setup.HasPendingSteps.Should().BeFalse();
    }

    [Test]
    public void Arranged_ShouldThrow_WhenStepsArePending()
    {
        var setup = new ProbeSetup().Create("a").Create("b");

        var act = () => setup.GetLastCreated();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("2 étape(s) du DbSetup en attente (Create, Create) : appeler RunAsync() avant de lire l'état arrangé.");
    }

    [Test]
    public async Task Arranged_ShouldRead_WhenNothingIsPending()
    {
        var setup = new ProbeSetup();

        await setup.Create("a").RunAsync();

        setup.GetLastCreated().Should().Be("a");
    }

    // Le setup-sonde : un fournisseur de services jamais sollicité, des
    // gestes qui n'écrivent qu'en mémoire.
    private sealed class ProbeSetup() : DbSetupBase(new EmptyServiceProvider())
    {
        public List<string> Created { get; } = [];

        public List<string> Touched { get; } = [];

        public ProbeSetup Create(string name)
        {
            Enqueue(
                name: nameof(Create),
                step: () =>
                {
                    Created.Add(name);

                    return Task.CompletedTask;
                }
            );

            return this;
        }

        public ProbeSetup Touch()
        {
            Enqueue(
                name: nameof(Touch),
                step: () =>
                {
                    Touched.Add(Created.Last());

                    return Task.CompletedTask;
                }
            );

            return this;
        }

        public ProbeSetup Fail(string message)
        {
            Enqueue(
                name: nameof(Fail),
                step: () => throw new ArgumentException(message)
            );

            return this;
        }

        public string GetLastCreated() => Arranged(() => Created.Last());
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
