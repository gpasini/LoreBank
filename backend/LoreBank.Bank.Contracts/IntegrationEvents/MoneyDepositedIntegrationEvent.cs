using LoreBank.SharedKernel.Contracts;

namespace LoreBank.Bank.Contracts.IntegrationEvents;

// Le jumeau publié de MoneyDepositedDomainEvent : primitives plates — la
// forme interne (VO) n'est pas un contrat, même grille que les paramètres
// d'exceptions. Il ne porte que ce que ses consommateurs consomment : pas de
// solde — une écriture comptable est un mouvement, pas un état.
[IntegrationEvent("bank.money-deposited")]
public sealed record MoneyDepositedIntegrationEvent(
    Guid AccountId,
    decimal Amount,
    string Currency
) : IIntegrationEvent;
