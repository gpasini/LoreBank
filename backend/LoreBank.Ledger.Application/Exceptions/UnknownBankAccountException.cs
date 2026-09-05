using LoreBank.SharedKernel.Domain.Exceptions;

namespace LoreBank.Ledger.Application.Exceptions;

// L'absence vue du Ledger : le compte Bank interrogé n'existe pas. L'id est
// une primitive — c'est celui du contrat publié, pas un VO du module.
public sealed class UnknownBankAccountException(Guid accountId) : NotFoundException(
    new() { ["accountId"] = accountId }
);
