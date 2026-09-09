using System.Transactions;
using LoreBank.SharedKernel.Application.Behaviors;
using LoreBank.SharedKernel.Test.Unit.Fakes;

namespace LoreBank.SharedKernel.Test.Unit.Behaviors;

[TestFixture]
[TestOf(typeof(TransactionBehavior<,>))]
public sealed class TransactionBehaviorTest
{
    [Test]
    public async Task Handle_ShouldReturnTheHandlerResponse_WhenHandlerSucceeds()
    {
        // Arrange

        var behavior = new TransactionBehavior<MutatingRequest, string>();

        // Act

        var response = await behavior.Handle(
            request: new MutatingRequest(),
            next: _ => Task.FromResult("ok"),
            cancellationToken: CancellationToken.None
        );

        // Assert

        response.Should().Be("ok");
    }

    [Test]
    public async Task Handle_ShouldOpenAnAmbientTransaction_WhenHandlingAMutatingRequest()
    {
        // Arrange

        var behavior = new TransactionBehavior<MutatingRequest, string>();
        Transaction? ambient = null;
        IsolationLevel? isolationLevel = null;

        // Act

        await behavior.Handle(
            request: new MutatingRequest(),
            next: _ =>
            {
                ambient = Transaction.Current;
                isolationLevel = ambient?.IsolationLevel;

                return Task.FromResult("ok");
            },
            cancellationToken: CancellationToken.None
        );

        // Assert

        ambient.Should().NotBeNull();
        isolationLevel.Should().Be(IsolationLevel.ReadCommitted);
    }

    [Test]
    public async Task Handle_ShouldLeaveNoAmbientTransaction_WhenHandlingCompletes()
    {
        var behavior = new TransactionBehavior<MutatingRequest, string>();

        await behavior.Handle(
            request: new MutatingRequest(),
            next: _ => Task.FromResult("ok"),
            cancellationToken: CancellationToken.None
        );

        Transaction.Current.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldPropagateTheException_WhenHandlerThrows()
    {
        // Arrange

        var behavior = new TransactionBehavior<MutatingRequest, string>();

        // Act

        var act = async () => await behavior.Handle(
            request: new MutatingRequest(),
            next: _ => throw new InvalidOperationException("boom"),
            cancellationToken: CancellationToken.None
        );

        // Assert

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Handle_ShouldNotOpenAnAmbientTransaction_WhenHandlingANonMutatingRequest()
    {
        // Arrange

        var behavior = new TransactionBehavior<NonMutatingRequest, string>();
        Transaction? ambient = null;

        // Act

        var response = await behavior.Handle(
            request: new NonMutatingRequest(),
            next: _ =>
            {
                ambient = Transaction.Current;

                return Task.FromResult("ok");
            },
            cancellationToken: CancellationToken.None
        );

        // Assert

        ambient.Should().BeNull();
        response.Should().Be("ok");
    }
}
