using LoreBank.Bank.Domain.Aggregates;
using LoreBank.Bank.Domain.Repositories;
using LoreBank.Bank.Test.Infrastructure.Builders;

namespace LoreBank.Bank.Test.Infrastructure.Setups;

// Une partielle par agrégat : un geste par commande (ADR 0030), chacun prend
// le builder de sa commande, comble le compte s'il n'est pas donné — le
// dernier créé, ou un compte par défaut s'il n'y en a aucun — et empile son
// étape. Les accesseurs rendent l'état arrangé : l'id, ou l'agrégat lui-même
// par le repository — pour un arrange, ou pour affirmer ce que le Domain a
// enregistré ; le cas nominal d'une commande reste vérifié par une query.
public partial class DbSetup
{
    private readonly List<BankAccountId> _bankAccountIds = [];

    public DbSetup CreateBankAccount(Action<OpenBankAccountCommandBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(CreateBankAccount),
            step: () => OpenAsync(configure)
        );

        return this;
    }

    public DbSetup Deposit(Action<DepositMoneyCommandBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(Deposit),
            step: async () =>
            {
                var builder = new DepositMoneyCommandBuilder();

                configure?.Invoke(builder);

                if (builder.AccountId is null) {
                    builder.Of(await LastOrNewAccountIdAsync());
                }

                await Sender.Send(builder.Build());
            }
        );

        return this;
    }

    public DbSetup Withdraw(Action<WithdrawMoneyCommandBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(Withdraw),
            step: async () =>
            {
                var builder = new WithdrawMoneyCommandBuilder();

                configure?.Invoke(builder);

                if (builder.AccountId is null) {
                    builder.Of(await LastOrNewAccountIdAsync());
                }

                await Sender.Send(builder.Build());
            }
        );

        return this;
    }

    public DbSetup Close(Action<CloseBankAccountCommandBuilder>? configure = null)
    {
        Enqueue(
            name: nameof(Close),
            step: async () =>
            {
                var builder = new CloseBankAccountCommandBuilder();

                configure?.Invoke(builder);

                if (builder.AccountId is null) {
                    builder.Of(await LastOrNewAccountIdAsync());
                }

                await Sender.Send(builder.Build());
            }
        );

        return this;
    }

    public BankAccountId GetLastBankAccountId() => Arranged(() => _bankAccountIds.Last());

    public Task<BankAccount> GetBankAccountAsync(BankAccountId? id = null) => Arranged(
        () => GetService<IBankAccountRepository>().GetRequiredByIdAsync(
            id: id ?? _bankAccountIds.Last(),
            cancellationToken: CancellationToken.None
        )
    );

    private async Task OpenAsync(Action<OpenBankAccountCommandBuilder>? configure)
    {
        var builder = new OpenBankAccountCommandBuilder();

        configure?.Invoke(builder);

        var accountId = await Sender.Send(builder.Build());

        _bankAccountIds.Add(BankAccountId.Hydrate(accountId));
    }

    private async Task<Guid> LastOrNewAccountIdAsync()
    {
        if (_bankAccountIds.Count == 0) {
            await OpenAsync(configure: null);
        }

        return _bankAccountIds.Last().Value;
    }
}
