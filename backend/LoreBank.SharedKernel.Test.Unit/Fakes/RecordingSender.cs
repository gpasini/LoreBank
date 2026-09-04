using MediatR;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

public sealed class RecordingSender : ISender
{
    public List<object> Sent { get; } = [];

    public Guid CreatedId { get; } = Guid.NewGuid();

    public Task<TResponse> Send<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default
    )
    {
        Sent.Add(request);

        return Task.FromResult((TResponse)(object)CreatedId);
    }

    public Task Send<TRequest>(
        TRequest request,
        CancellationToken cancellationToken = default
    ) where TRequest : IRequest
    {
        Sent.Add(request);

        return Task.CompletedTask;
    }

    public Task<object?> Send(
        object request,
        CancellationToken cancellationToken = default
    )
    {
        Sent.Add(request);

        return Task.FromResult<object?>(null);
    }

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        IStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public IAsyncEnumerable<object?> CreateStream(
        object request,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();
}
