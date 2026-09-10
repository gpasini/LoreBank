using LoreBank.Bank.Application.Commands.CloseBankAccount;

namespace LoreBank.Bank.Test.Infrastructure.Builders;

public sealed class CloseBankAccountCommandBuilder
{
    public Guid? AccountId { get; private set; }

    public CloseBankAccountCommandBuilder Of(Guid accountId)
    {
        AccountId = accountId;
        return this;
    }

    public CloseBankAccountCommand Build() => new(
        AccountId ?? throw new InvalidOperationException("La clôture n'a pas de compte : Of(accountId).")
    );
}
