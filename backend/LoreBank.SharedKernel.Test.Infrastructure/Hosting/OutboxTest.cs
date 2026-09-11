using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le store d'une outbox, éprouvé par son interface : le seul endroit qui
// sache comment une ligne est faite. Le store se construit sur un DbContext
// nu — aucun conteneur, aucun scope à monter ici, c'est tout l'intérêt de
// la forme — et ces tests écrivent pour de vrai dans probe.__outbox, d'où
// BaseHostTest (un TransactionScope de fixture masquerait les commits que
// le store fait) et le nettoyage encadrant.
[TestFixture]
[TestOf(typeof(Outbox))]
public sealed class OutboxTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private const string Discriminant = "probe.probe-stored";

    private const string Payload = """{"label":"stored"}""";

    // La colonne est jsonb : Postgres range une valeur analysée et la rend
    // sous sa forme canonique, espace après les deux-points compris. Ce que
    // le store relit n'est donc pas la chaîne donnée, c'est le même JSON.
    private const string StoredPayload = """{"label": "stored"}""";

    private readonly static TimeSpan Lease = TimeSpan.FromMinutes(5);

    [SetUp]
    public async Task SetUp() => await ProbeOutbox.CleanAsync(Factory);

    [TearDown]
    public async Task TearDown() => await ProbeOutbox.CleanAsync(Factory);

    [Test]
    public async Task InsertAsync_ShouldWriteAPendingRow_WhenGivenAnEntry()
    {
        // Act

        var id = await InsertAsync();

        // Assert

        var row = await OnOutboxAsync(outbox => outbox.ReadAllAsync(CancellationToken.None));

        row.Should().ContainSingle();
        row[0].Id.Should().Be(id);
        row[0].Discriminant.Should().Be(Discriminant);
        row[0].Payload.Should().Be(StoredPayload);
        row[0].Attempts.Should().Be(0);
        row[0].Dispatched.Should().BeFalse();
        row[0].Poisoned.Should().BeFalse();
        row[0].Reserved.Should().BeFalse();
    }

    [Test]
    public async Task InsertAsync_ShouldKeepTheResourceOutOfThePayload_WhenTheEntryNamesOne()
    {
        // Arrange

        var resourceId = Guid.NewGuid();

        // Act

        await InsertAsync(
            resourceKind: "probe-thing",
            resourceId: resourceId
        );

        // Assert — le suiveur de Signal lit deux colonnes, jamais le payload.

        var row = (await OnOutboxAsync(outbox => outbox.ReadAllAsync(CancellationToken.None))).Single();

        row.ResourceKind.Should().Be("probe-thing");
        row.ResourceId.Should().Be(resourceId);
        row.Payload.Should().NotContain("probe-thing");
    }

    [Test]
    public async Task ReserveAsync_ShouldReturnThePendingRow_WhenNoLeaseIsHeld()
    {
        // Arrange

        var id = await InsertAsync();

        // Act

        var reserved = await ReserveAsync();

        // Assert

        reserved.Should().ContainSingle();
        reserved[0].Id.Should().Be(id);
        reserved[0].Discriminant.Should().Be(Discriminant);
        reserved[0].Payload.Should().Be(StoredPayload);
        reserved[0].Attempts.Should().Be(0);
    }

    [Test]
    public async Task ReserveAsync_ShouldReturnNothing_WhenTheRowIsAlreadyReserved()
    {
        // Arrange — la première passe s'approprie la ligne pour la durée du bail.

        await InsertAsync();
        await ReserveAsync();

        // Act

        var second = await ReserveAsync();

        // Assert — une autre instance qui dépile la même outbox passe à côté.

        second.Should().BeEmpty();
    }

    [Test]
    public async Task ReserveAsync_ShouldReturnNothing_WhenTheRowIsAlreadyDispatched()
    {
        // Arrange

        var id = await InsertAsync();

        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: id,
                cancellationToken: CancellationToken.None
            )
        );

        // Act & Assert

        (await ReserveAsync()).Should().BeEmpty();
    }

    [Test]
    public async Task MarkDispatchedAsync_ShouldMarkTheRowAndReleaseItsLease_WhenTheRowWasReserved()
    {
        // Arrange

        var id = await InsertAsync();

        await ReserveAsync();

        // Act

        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: id,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert

        var row = (await OnOutboxAsync(outbox => outbox.ReadAllAsync(CancellationToken.None))).Single();

        row.Dispatched.Should().BeTrue();
        row.Reserved.Should().BeFalse();
    }

    [Test]
    public async Task RecordFailureAsync_ShouldCountTheAttemptAndKeepTheRowPending_WhenItIsNotPoisoned()
    {
        // Arrange

        var id = await InsertAsync();

        await ReserveAsync();

        // Act

        await RecordFailureAsync(
            id: id,
            attempts: 1,
            poisoned: false
        );

        // Assert — la ligne repart en attente, son bail rendu, avec sa dernière erreur.

        var row = (await OnOutboxAsync(outbox => outbox.ReadAllAsync(CancellationToken.None))).Single();

        row.Attempts.Should().Be(1);
        row.Poisoned.Should().BeFalse();
        row.Dispatched.Should().BeFalse();
        row.Reserved.Should().BeFalse();
        row.LastError.Should().Be("le handler a échoué");
    }

    [Test]
    public async Task RecordFailureAsync_ShouldPoisonTheRow_WhenTheCallerSaysSo()
    {
        // Arrange — la décision vient du processor, qui tient MaxAttempts ;
        // le store écrit ce qu'on lui donne.

        var id = await InsertAsync();

        // Act

        await RecordFailureAsync(
            id: id,
            attempts: 5,
            poisoned: true
        );

        // Assert

        var row = (await OnOutboxAsync(outbox => outbox.ReadAllAsync(CancellationToken.None))).Single();

        row.Poisoned.Should().BeTrue();
        row.Attempts.Should().Be(5);
    }

    [Test]
    public async Task MeasureAsync_ShouldCountPendingAndPoisonedRows_WhenBothArePresent()
    {
        // Arrange

        await InsertAsync();

        var poisoned = await InsertAsync();

        await RecordFailureAsync(
            id: poisoned,
            attempts: 5,
            poisoned: true
        );

        // Act

        var depth = await OnOutboxAsync(outbox => outbox.MeasureAsync(CancellationToken.None));

        // Assert — une ligne poison n'est plus en attente.

        depth.Pending.Should().Be(1);
        depth.Poisoned.Should().Be(1);
    }

    [Test]
    public async Task PurgeAsync_ShouldDeleteDispatchedRowsPastRetention_AndKeepTheOthers()
    {
        // Arrange — une ligne livrée puis vieillie de huit jours, une ligne
        // livrée d'aujourd'hui, une ligne poison vieillie autant.

        var old = await InsertAsync();
        var fresh = await InsertAsync();
        var poisoned = await InsertAsync();

        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: old,
                cancellationToken: CancellationToken.None
            )
        );
        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: fresh,
                cancellationToken: CancellationToken.None
            )
        );
        await RecordFailureAsync(
            id: poisoned,
            attempts: 5,
            poisoned: true
        );

        await ProbeOutbox.BackdateAsync(
            factory: Factory,
            id: old,
            by: TimeSpan.FromDays(8)
        );
        await ProbeOutbox.BackdateAsync(
            factory: Factory,
            id: poisoned,
            by: TimeSpan.FromDays(8)
        );

        // Act

        await OnOutboxAsync(outbox => outbox.PurgeAsync(
                retentionDays: 7,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert — une ligne poison n'est jamais purgée (ADR 0021).

        var ids = (await OnOutboxAsync(outbox => outbox.ReadAllAsync(CancellationToken.None)))
            .Select(row => row.Id)
            .ToList();

        ids.Should().BeEquivalentTo(new[] { fresh, poisoned });
    }

    [Test]
    public async Task ReadDispatchedSinceAsync_ShouldReturnOnlyRowsThatNameAResource_WhenSomeDoNot()
    {
        // Arrange

        var since = await OnOutboxAsync(outbox => outbox.NowAsync(CancellationToken.None));
        var resourceId = Guid.NewGuid();

        var signalling = await InsertAsync(
            resourceKind: "probe-thing",
            resourceId: resourceId
        );
        var silent = await InsertAsync();

        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: signalling,
                cancellationToken: CancellationToken.None
            )
        );
        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: silent,
                cancellationToken: CancellationToken.None
            )
        );

        // Act

        var dispatched = await OnOutboxAsync(outbox => outbox.ReadDispatchedSinceAsync(
                since: since,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert — le Signal est opt-in par l'event, qui nomme sa ressource.

        dispatched.Should().ContainSingle();
        dispatched[0].Id.Should().Be(signalling);
        dispatched[0].ResourceKind.Should().Be("probe-thing");
        dispatched[0].ResourceId.Should().Be(resourceId);
    }

    [Test]
    public async Task ReadDispatchedSinceAsync_ShouldReturnNothing_WhenTheCursorIsPastTheMarking()
    {
        // Arrange

        var id = await InsertAsync(
            resourceKind: "probe-thing",
            resourceId: Guid.NewGuid()
        );

        await OnOutboxAsync(outbox => outbox.MarkDispatchedAsync(
                id: id,
                cancellationToken: CancellationToken.None
            )
        );

        var after = await OnOutboxAsync(outbox => outbox.NowAsync(CancellationToken.None));

        // Act & Assert

        (await OnOutboxAsync(outbox => outbox.ReadDispatchedSinceAsync(
                since: after,
                cancellationToken: CancellationToken.None
            )
        )).Should().BeEmpty();
    }

    [Test]
    public async Task NowAsync_ShouldReturnTheClockThatStampsTheTable_WhenCalled()
    {
        // Act — l'horloge du curseur est celle de la base, pas celle du process.

        var before = await OnOutboxAsync(outbox => outbox.NowAsync(CancellationToken.None));

        await InsertAsync();

        var after = await OnOutboxAsync(outbox => outbox.NowAsync(CancellationToken.None));

        // Assert

        after.Should().BeOnOrAfter(before);
    }

    [Test]
    public async Task EnsureTableAsync_ShouldAddWhatCameLater_WhenTheTableHasItsOriginalShape()
    {
        // Arrange — la forme d'origine : ni bail, ni traceparent, ni index de
        // purge, ni ressource de Signal. Le DDL du socle est idempotent et ne
        // fait que s'ajouter (ADR 0021).

        await ProbeSql.ExecuteAsync(
            factory: Factory,
            sql: """
                 ALTER TABLE probe.__outbox DROP COLUMN IF EXISTS reserved_until;
                 ALTER TABLE probe.__outbox DROP COLUMN IF EXISTS trace_parent;
                 ALTER TABLE probe.__outbox DROP COLUMN IF EXISTS resource_kind;
                 ALTER TABLE probe.__outbox DROP COLUMN IF EXISTS resource_id;
                 DROP INDEX IF EXISTS probe.ix___outbox_dispatched;
                 """
        );

        // Act

        using (var scope = Factory.Services.CreateScope()) {
            await Outbox.EnsureTableAsync(
                dbContext: scope.ServiceProvider.GetRequiredService<ProbeDbContext>(),
                cancellationToken: CancellationToken.None
            );
        }

        // Assert

        (await ProbeSql.CountAsync(
            factory: Factory,
            sql: """
                 SELECT count(*) FROM information_schema.columns
                 WHERE table_schema = 'probe' AND table_name = '__outbox'
                   AND column_name IN ('reserved_until', 'trace_parent', 'resource_kind', 'resource_id')
                 """
        )).Should().Be(4);

        (await ProbeSql.CountAsync(
            factory: Factory,
            sql: """
                 SELECT count(*) FROM pg_indexes
                 WHERE schemaname = 'probe' AND indexname = 'ix___outbox_dispatched'
                 """
        )).Should().Be(1);
    }

    private static Task<T> OnOutboxAsync<T>(Func<Outbox, Task<T>> action) =>
        ProbeSql.OnOutboxAsync(
            factory: Factory,
            action: action
        );

    private static Task OnOutboxAsync(Func<Outbox, Task> action) =>
        ProbeSql.OnOutboxAsync(
            factory: Factory,
            action: async outbox =>
            {
                await action(outbox);

                return 0;
            }
        );

    private static async Task<Guid> InsertAsync(
        string? resourceKind = null,
        Guid? resourceId = null
    )
    {
        var id = Guid.NewGuid();

        await OnOutboxAsync(outbox => outbox.InsertAsync(
                entry: new Outbox.Entry(
                    Id: id,
                    Discriminant: Discriminant,
                    Payload: Payload,
                    TraceParent: null,
                    ResourceKind: resourceKind,
                    ResourceId: resourceId
                ),
                cancellationToken: CancellationToken.None
            )
        );

        return id;
    }

    private static Task<IReadOnlyList<Outbox.Reserved>> ReserveAsync() =>
        OnOutboxAsync(outbox => outbox.ReserveAsync(
                lease: Lease,
                batchSize: 100,
                cancellationToken: CancellationToken.None
            )
        );

    private static Task RecordFailureAsync(
        Guid id,
        int attempts,
        bool poisoned
    ) =>
        OnOutboxAsync(outbox => outbox.RecordFailureAsync(
                id: id,
                attempts: attempts,
                lastError: "le handler a échoué",
                delaySeconds: 0,
                poisoned: poisoned,
                cancellationToken: CancellationToken.None
            )
        );
}
