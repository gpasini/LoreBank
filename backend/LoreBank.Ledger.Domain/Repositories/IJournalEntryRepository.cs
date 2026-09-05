using LoreBank.Ledger.Domain.Aggregates;

namespace LoreBank.Ledger.Domain.Repositories;

public interface IJournalEntryRepository
{
    // Non nullable : l'absence est une erreur métier, le repository lève la
    // NotFoundException du module. Pas de variante nullable — une sonde
    // d'existence n'est pas un usage.
    Task<JournalEntry> GetRequiredByIdAsync(
        JournalEntryId id,
        CancellationToken cancellationToken
    );

    Task SaveAsync(
        JournalEntry entry,
        CancellationToken cancellationToken
    );
}
