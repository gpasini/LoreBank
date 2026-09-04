using LoreBank.Bank.Application.Exceptions;
using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.Bank.Infrastructure.Persistence;
using LoreBank.SharedKernel.Domain.Exceptions;
using LoreBank.SharedKernel.Infrastructure.Repositories;

namespace LoreBank.Bank.Infrastructure.Repositories;

// Chargement, mini-unit-of-work et dispatch des events vivent dans
// ModuleRepository — ne reste que ce que la base ne peut pas savoir.
public sealed class BankAccountRepository(BankDbContext context)
    : ModuleRepository<BankAccount, BankAccountId>(context), IBankAccountRepository
{
    protected override NotFoundException NotFound(BankAccountId id) => new BankAccountNotFoundException(id);
}
