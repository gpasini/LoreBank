---
name: nouveau-domain-event-handler
description: À utiliser avant d'écrire une réaction à un domain event — effet de bord à déclencher quand un fait métier survient (notification, courrier, mise à jour d'un autre concept) — ou quand une telle réaction est en train d'être codée dans l'agrégat lui-même.
---

# Nouveau domain event handler

## Principe

L'agrégat émet des faits ; il ne sait pas qui réagit. Les réactions vivent dans
des handlers du Domain (`EventHandlers/`), qui ne dépendent que d'abstractions :
leurs effets de bord passent par des **ports** (interfaces dans `Services/`)
que l'Infrastructure implémentera.

## Recette

1. Si l'effet de bord touche le monde extérieur, définir le port dans
   `Services/` du Domain : interface, méthodes `Task …Async(…, CancellationToken)`.
2. Handler `sealed` dans `EventHandlers/` :

```csharp
public sealed class BankAccountOpenedDomainEventHandler(IWelcomeLetterSender welcomeLetterSender)
    : IDomainEventHandler<BankAccountOpened>
{
    public Task HandleAsync(
        BankAccountOpened domainEvent,
        CancellationToken cancellationToken
    ) => welcomeLetterSender.SendAsync(
        iban: domainEvent.Iban,
        cancellationToken: cancellationToken
    );
}
```

3. Si le handler a besoin d'une donnée absente de l'event, enrichir l'event à
   l'émission plutôt que de faire recharger l'agrégat par le handler.

## Pièges

- Dépendre d'une implémentation concrète (SMTP, EF…) dans le Domain : toujours
  un port.
- Mettre la réaction dans l'agrégat émetteur : il émet, il ne réagit pas.
- Handler qui décide d'une règle métier de l'agrégat : les invariants restent
  dans l'agrégat ; le handler orchestre.
- Avaler le `CancellationToken`.

## Câblage dans le socle

Le dispatch existe : le `SaveChangesAsync` du `DbContext` du module ramasse les
events des entités trackées, écrit, puis les remet à `IDomainEventDispatcher` —
**dans la transaction de la commande** (un handler qui échoue l'annule ; voir
CLAUDE.md « Conventions du domaine » pour les limites). Deux conditions pour
qu'un handler soit appelé, toutes deux à échec silencieux si oubliées :

1. Le `Module` Autofac de l'Infrastructure du module scanne son assembly Domain
   (`AsClosedTypesOf(typeof(IDomainEventHandler<>))`, voir
   `BankInfrastructureModule`).
2. Le `DbContext` du module surcharge `SaveChangesAsync` comme `BankDbContext`.

En unitaire, un handler se teste en l'appelant directement avec un event et un
faux port ; en intégration, via le vrai use case qui émet l'event (voir
`OpenBankAccountTest` et `TransactionRollbackTest`).
