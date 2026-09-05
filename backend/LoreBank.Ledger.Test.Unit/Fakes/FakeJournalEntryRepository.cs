using LoreBank.Ledger.Application.Exceptions;
using LoreBank.Ledger.Domain.Aggregates;
using LoreBank.Ledger.Domain.Repositories;

namespace LoreBank.Ledger.Test.Unit.Fakes;

public sealed class FakeJournalEntryRepository : IJournalEntryRepository
{
    public List<JournalEntry> Saved { get; } = [];

    public Task<JournalEntry> GetRequiredByIdAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    ) => Task.FromResult(
        Saved.SingleOrDefault(entry => entry.Id.Equals(id))
        ?? throw new JournalEntryNotFoundException(id)
    );

    public Task SaveAsync(
        JournalEntry entry,
        CancellationToken cancellationToken
    )
    {
        Saved.Add(entry);

        return Task.CompletedTask;
    }
}
