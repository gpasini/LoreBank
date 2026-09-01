using LoreBank.SharedKernel.Domain.Entities;

namespace LoreBank.SharedKernel.Domain.Aggregates;

public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id) where TId : notnull;
