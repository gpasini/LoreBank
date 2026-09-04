using LoreBank.SharedKernel.Domain.Events;
using LoreBank.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LoreBank.SharedKernel.Test.Unit.Fakes;

// Prouve que ConfigureModule est optionnel : aucun override, le modèle se
// construit avec le schéma seul.
public sealed class BareModuleDbContext(
    DbContextOptions<BareModuleDbContext> options,
    IDomainEventDispatcher dispatcher
) : ModuleDbContext(
    options: options,
    dispatcher: dispatcher,
    schema: "bare"
);
