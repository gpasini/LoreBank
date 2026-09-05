using System.Transactions;
using LoreBank.SharedKernel.Contracts;
using LoreBank.SharedKernel.Infrastructure.IntegrationEvents;
using LoreBank.SharedKernel.Test.Infrastructure.Probes;
using LoreBank.SharedKernel.Test.Infrastructure.Setups;
using Microsoft.Extensions.DependencyInjection;

namespace LoreBank.SharedKernel.Test.Infrastructure.Hosting;

// BaseHostTest : ces tests observent des commits et des rollbacks réels — un
// TransactionScope ambiant de fixture fausserait précisément ce qu'ils
// prouvent.
[TestFixture]
[TestOf(typeof(OutboxPublisher))]
public sealed class OutboxPublisherTest : BaseHostTest<SharedKernelWebAppFactory>
{
    [SetUp]
    public async Task SetUp() => await ProbeOutbox.CleanAsync(Factory);

    [TearDown]
    public async Task TearDown() => await ProbeOutbox.CleanAsync(Factory);

    [Test]
    public async Task PublishAsync_ShouldWriteNothing_WhenTheAmbientTransactionRollsBack()
    {
        // Act

        using (new TransactionScope(
                   scopeOption: TransactionScopeOption.Required,
                   transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                   asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
               )) {
            await PublishAsync(new ProbeIntegrationEvent(
                ThingId: Guid.NewGuid(),
                Label: "rollback"
            ));

            // Pas de Complete : le scope se dispose en rollback.
        }

        // Assert

        // L'event part avec la transaction de la commande ou pas du tout —
        // toute la promesse de l'outbox tient dans cette ligne absente.
        (await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "bank.probe-happened"
        )).Should().BeNull();
    }

    [Test]
    public async Task PublishAsync_ShouldWriteThePendingRow_WhenTheAmbientTransactionCompletes()
    {
        // Act

        using (var transaction = new TransactionScope(
                   scopeOption: TransactionScopeOption.Required,
                   transactionOptions: new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted },
                   asyncFlowOption: TransactionScopeAsyncFlowOption.Enabled
               )) {
            await PublishAsync(new ProbeIntegrationEvent(
                ThingId: Guid.NewGuid(),
                Label: "commit"
            ));

            transaction.Complete();
        }

        // Assert

        var row = await ProbeOutbox.FindRowAsync(
            factory: Factory,
            discriminant: "bank.probe-happened"
        );

        row.Should().NotBeNull();
        row.Attempts.Should().Be(0);
        row.Dispatched.Should().BeFalse();
        row.Poisoned.Should().BeFalse();
    }

    [Test]
    public async Task PublishAsync_ShouldThrow_WhenTheDiscriminantNamesNoMountedModule()
    {
        var act = () => PublishAsync(new ProbeOrphanIntegrationEvent(Guid.NewGuid()));

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*nowhere*HostModules.All*");
    }

    private static async Task PublishAsync(IIntegrationEvent integrationEvent)
    {
        using var scope = Factory.Services.CreateScope();

        await scope.ServiceProvider
            .GetRequiredService<IIntegrationEventPublisher>()
            .PublishAsync(
                integrationEvent: integrationEvent,
                cancellationToken: CancellationToken.None
            );
    }
}
