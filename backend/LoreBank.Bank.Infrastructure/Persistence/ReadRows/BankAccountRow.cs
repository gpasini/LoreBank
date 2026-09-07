namespace LoreBank.Bank.Infrastructure.Persistence.ReadRows;

// Le miroir plat de bank_accounts, réservé à la lecture : des primitives,
// jamais un VO — une row keyless n'est ni suivie ni écrite, et tous les
// readers du module la partagent (une row par table, pas par query).
public sealed record BankAccountRow(
    Guid Id,
    string Iban,
    decimal BalanceAmount,
    string BalanceCurrency,
    bool IsClosed
);
