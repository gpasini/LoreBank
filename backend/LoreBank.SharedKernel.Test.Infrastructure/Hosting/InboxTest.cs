using LoreBank.Probe.Infrastructure.Persistence;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// Le store de l'inbox d'un module consommateur : c'est lui qui rend la
// livraison at-least-once idempotente. Le processor l'écrit dans la
// transaction de son handler — ici, hors transaction, on ne prouve que les
// gestes eux-mêmes.
[TestFixture]
[TestOf(typeof(Inbox))]
public sealed class InboxTest : BaseHostTest<SharedKernelWebAppFactory>
{
    private const string Handler = "LoreBank.Probe.ProbeHandler";

    [SetUp]
    public async Task SetUp() => await ProbeOutbox.CleanAsync(Factory);

    [TearDown]
    public async Task TearDown() => await ProbeOutbox.CleanAsync(Factory);

    [Test]
    public async Task IsHandledAsync_ShouldBeFalse_WhenNothingIsJournalled()
    {
        // Act & Assert

        (await IsHandledAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task MarkHandledAsync_ShouldMakeTheEventHandled_WhenTheSameHandlerAsks()
    {
        // Arrange

        var eventId = Guid.NewGuid();

        // Act

        await OnInboxAsync(inbox => inbox.MarkHandledAsync(
                eventId: eventId,
                handler: Handler,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert — c'est cette trace qui rend un rejeu inoffensif.

        (await IsHandledAsync(eventId)).Should().BeTrue();
    }

    [Test]
    public async Task IsHandledAsync_ShouldBeFalse_WhenAnotherHandlerHasTreatedTheSameEvent()
    {
        // Arrange — la clé est la paire (event, handler) : deux consommateurs
        // du même event se journalisent séparément.

        var eventId = Guid.NewGuid();

        await OnInboxAsync(inbox => inbox.MarkHandledAsync(
                eventId: eventId,
                handler: Handler,
                cancellationToken: CancellationToken.None
            )
        );

        // Act & Assert

        (await IsHandledAsync(
            eventId: eventId,
            handler: "LoreBank.Probe.OtherProbeHandler"
        )).Should().BeFalse();
    }

    [Test]
    public async Task PurgeAsync_ShouldDeleteTracesPastRetention_AndKeepTheFreshOnes()
    {
        // Arrange

        var old = Guid.NewGuid();
        var fresh = Guid.NewGuid();

        await OnInboxAsync(inbox => inbox.MarkHandledAsync(
                eventId: old,
                handler: Handler,
                cancellationToken: CancellationToken.None
            )
        );
        await OnInboxAsync(inbox => inbox.MarkHandledAsync(
                eventId: fresh,
                handler: Handler,
                cancellationToken: CancellationToken.None
            )
        );

        await ProbeOutbox.BackdateAsync(
            factory: Factory,
            id: old,
            by: TimeSpan.FromDays(8)
        );

        // Act

        await OnInboxAsync(inbox => inbox.PurgeAsync(
                retentionDays: 7,
                cancellationToken: CancellationToken.None
            )
        );

        // Assert

        (await IsHandledAsync(old)).Should().BeFalse();
        (await IsHandledAsync(fresh)).Should().BeTrue();
    }

    [Test]
    public async Task EnsureTableAsync_ShouldAddWhatCameLater_WhenTheTableHasItsOriginalShape()
    {
        // Arrange — la forme d'origine, sans l'index de purge.

        await ProbeSql.ExecuteAsync(
            factory: Factory,
            sql: "DROP INDEX IF EXISTS probe.ix___inbox_handled;"
        );

        // Act

        using (var scope = Factory.Services.CreateScope()) {
            await Inbox.EnsureTableAsync(
                dbContext: scope.ServiceProvider.GetRequiredService<ProbeDbContext>(),
                cancellationToken: CancellationToken.None
            );
        }

        // Assert

        (await ProbeSql.CountAsync(
            factory: Factory,
            sql: """
                 SELECT count(*) FROM pg_indexes
                 WHERE schemaname = 'probe' AND indexname = 'ix___inbox_handled'
                 """
        )).Should().Be(1);
    }

    private static Task<T> OnInboxAsync<T>(Func<Inbox, Task<T>> action) =>
        ProbeSql.OnInboxAsync(
            factory: Factory,
            action: action
        );

    private static Task OnInboxAsync(Func<Inbox, Task> action) =>
        ProbeSql.OnInboxAsync(
            factory: Factory,
            action: async inbox =>
            {
                await action(inbox);

                return 0;
            }
        );

    private static Task<bool> IsHandledAsync(
        Guid eventId,
        string handler = Handler
    ) =>
        OnInboxAsync(inbox => inbox.IsHandledAsync(
                eventId: eventId,
                handler: handler,
                cancellationToken: CancellationToken.None
            )
        );
}
