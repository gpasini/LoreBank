---
name: nouveau-domain-event-handler
description: À utiliser avant d'écrire une réaction à un domain event — effet de bord à déclencher quand un fait métier survient (notification, courrier, mise à jour d'un autre concept) — ou quand une telle réaction est en train d'être codée dans l'agrégat lui-même.
---

# Nouveau domain event handler

## Principe

L'agrégat émet des faits ; il ne sait pas qui réagit. Les réactions vivent
dans des handlers du Domain (`EventHandlers/`) qui ne dépendent que de
**ports** — interfaces dans `Services/`, implémentées par l'Infrastructure.

## Recette

1. **Le squelette** : le port dans `Services/` du Domain (interface, méthodes
   `Task …Async(…, CancellationToken)`), et le handler de l'étape 3 réduit à
   sa signature — `throw new NotImplementedException()`. Juste de quoi
   compiler.
2. **Le test unitaire du handler**, écrit maintenant : appeler le handler
   avec un event et un fake du port
   (`BankAccountOpenedDomainEventHandlerTest`, `FakeWelcomeLetterSender`),
   et affirmer que le port est appelé avec les bons arguments. **Il doit
   rougir** — le RED de l'ADR 0032 — avant que le handler ne délègue quoi
   que ce soit : c'est le test qui dit quels arguments le port reçoit, pas
   le handler qui le dicte au test.
3. Le handler, `sealed`, dans `EventHandlers/` :

```csharp
public sealed class BankAccountOpenedDomainEventHandler(IWelcomeLetterSender welcomeLetterSender)
    : IDomainEventHandler<BankAccountOpenedDomainEvent>
{
    public Task HandleAsync(
        BankAccountOpenedDomainEvent domainEvent,
        CancellationToken cancellationToken
    ) => welcomeLetterSender.SendAsync(
        iban: domainEvent.Iban,
        cancellationToken: cancellationToken
    );
}
```

4. Une donnée absente de l'event ? Enrichir l'event à l'émission — le handler
   travaille avec ce que l'event porte, il ne recharge pas l'agrégat. Vaut
   pour l'Instant (ADR 0024) : une réaction datée reprend celui du fait
   (`OpenedAt`, `RecordedAt`), elle ne prend jamais `TimeProvider` — deux
   dates pour un seul fait seraient un mensonge.
5. L'implémentation du port dans `Services/` de l'Infrastructure, enregistrée
   dans le `Module` Autofac du module (voir `LoggingWelcomeLetterSender` et
   `BankInfrastructureModule`).
6. Le tour d'intégration, après coup : passer par le use case qui émet
   l'event (`OpenBankAccountTest`) — il épingle le câblage, il ne spécifie
   pas le handler.

## Câblage dans le socle

Le dispatch existe et ne demande rien au module : le `SaveChangesAsync` de
`ModuleDbContext` ramasse les events des entités trackées, écrit, puis les
remet à `IDomainEventDispatcher` — **dans la transaction de la commande**. Un
handler qui échoue annule la commande entière : un effet de bord métier qui
rate ne laisse pas derrière lui un fait métier enregistré. Les handlers sont
enregistrés par l'hôte, qui scanne la `DomainAssembly` de chaque
`IHostModule` : un handler posé dans `EventHandlers/` est câblé d'office.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Le handler se résout dans le conteneur de l'hôte | `ModuleCompositionTest` |
| Le handler vit dans la `DomainAssembly` — ailleurs, il échapperait au scan | `ModuleCompositionTest` |
| Un handler qui échoue annule la commande | `TransactionRollbackTest`, `ModuleDbContextTest` |
| Aucune implémentation concrète dans le Domain | le graphe de projets — le Domain ne référence aucune infrastructure, la dépendance ne compile pas |
| Le handler ne lit pas l'horloge (ADR 0024) | le build : l'analyseur d'API bannies rougit en `RS0030` dans tout projet `.Domain` |

## Pièges

- Le handler orchestre ; les invariants restent dans l'agrégat émetteur.
- Propager le `CancellationToken` jusqu'au port.
- Handler court, de préférence in-process : il tourne dans la transaction de
  la commande, les lignes touchées restent verrouillées le temps de son
  exécution.
- Un effet de bord externe irréversible veut une **outbox**, pas ce
  mécanisme : la garantie ne joue que dans un sens — un handler réussi suivi
  d'un commit qui échoue laisse l'effet fait, sans fait métier derrière.

## Avant de terminer

Build sans warning, le test unitaire de l'étape 2 **rouge d'abord, puis**
vert (event reçu → port appelé avec les bons arguments), et
`ModuleCompositionTest` vert — il prouve la résolution et le rangement.
