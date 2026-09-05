using LoreBank.SharedKernel.Application;

namespace LoreBank.Ledger.Application.Queries.GetBankAccountLedger;

public sealed record GetBankAccountLedgerQuery(Guid AccountId) : IQuery<BankAccountLedgerResult>;
