---
name: nouvel-agregat
description: À utiliser avant de créer ou faire évoluer un agrégat du domaine — nouveau concept métier avec identité et cycle de vie (compte, virement, dossier…), ajout d'une transition d'état, ou déplacement de logique métier vers le domaine.
---

# Nouvel agrégat

## Principe

L'agrégat est la frontière de cohérence : constructeur privé, factory de
naissance, invariants dans les méthodes de transition, un domain event par
transition. Impossible d'obtenir un agrégat invalide — et impossible d'en
obtenir un sans passer par la factory.

## Recette

1. **Id typé** dans `Aggregates/` : `sealed class XxxId(Guid value) :
   SimpleValueObject<Guid>` avec factory `New()`.
2. **Classe** `sealed class Xxx : AggregateRoot<XxxId>`, constructeur
   `private` — plus la concession EF : un second constructeur privé sans
   paramètre (`base(null!)`, propriétés à `null!`) réservé à la
   matérialisation.
3. **Naissance** : factory statique (`Open`, `Create`…) qui construit, puis
   émet l'event de naissance via `AddDomainEvent`.
4. **Transitions** : une méthode par opération métier (`Deposit`, `Close`…) —
   gardes de cycle de vie, invariants avec exception dédiée, mutation
   (`private set`), event en dernier. Les gardes récurrentes se factorisent en
   méthode privée (`EnsureIsOpen`).
5. **Paramètres typés au plus juste** : un montant d'opération est un
   `PositiveMoney`, pas un `Money` nu — un montant négatif qui inverserait le
   sens de l'opération devient inexprimable. Un primitif qui arrive nu passe
   par la skill `nouveau-value-object`.
6. **Auteur et date reçus, jamais demandés** : une transition qui enregistre
   son auteur prend l'Acteur (`Actor`, ADR 0023) en paramètre, une transition
   qui date un fait prend l'Instant (`DateTimeOffset`, ADR 0024) — le Domain
   ne lit ni le principal ni l'horloge, c'est le handler qui les fournit
   (skill `nouvelle-commande`). L'event de la transition les porte
   (`BankAccountOpenedDomainEvent.OpenedBy`, `.OpenedAt`). Pas de VO pour
   l'Instant, pas de port `IClock`.
7. **Events** : `sealed record` nommé au passé et suffixé `DomainEvent`
   (`BankAccountOpenedDomainEvent`),
   implémentant `IDomainEvent`, dans `Events/`, portant l'id de l'agrégat et
   les données utiles au consommateur. Un fait qu'un autre module consomme
   a un jumeau publié dans les Contrats du module (`…IntegrationEvent`,
   primitives plates, `[IntegrationEvent("<module>.<fait>")]`), mappé par
   un domain event handler vers `IIntegrationEventPublisher` (ADR 0014).
   Un fait que **les clients** doivent apprendre (ADR 0026,
   `docs/signaux.md`) : le jumeau implémente `ISignalsClients` et nomme sa
   ressource — `ResourceKind` stable en kebab-case (`bank-account`),
   `ResourceId` — rien d'autre à câbler ; le front abonné à cette ressource
   relit son `GET`. Signaler commence par publier : pas de Signal sans
   jumeau.
8. **Exceptions** : une classe `sealed : DomainException` par invariant, dans
   `Exceptions/`. Le constructeur peut prendre des VO, mais le dictionnaire
   passé à la base ne porte que des **primitives** à clés camelCase
   (`["balance"] = balance.Amount`) — jamais de texte.
9. **Tests** : une classe par méthode (`Domain/<Agrégat>/<Méthode>Test.cs`) —
   cycle nominal, chaque invariant rejeté avec son exception, events émis dans
   l'ordre, l'Acteur et l'Instant reçus relus tels quels (sans fake : ce sont
   des valeurs) — et une ligne par exception dans l'`ExceptionCodesTest` du
   module.
10. **Sa partielle de `DbSetup`** (`Test.Infrastructure/Setups/DbSetup.<Agrégat>.cs`,
    ADR 0030) : la liste des ids créés, `GetLast<Agrégat>Id()` et
    `Get<Agrégat>Async()` via le repository — tous deux par `Arranged` ; ses
    gestes arrivent avec ses commandes (skill `nouvelle-commande`).

## Répartition des invariants

| Invariant | Où |
|---|---|
| Format/cohérence d'une valeur | Dans le VO — le construire suffit (skill `nouveau-value-object`) |
| Règle sur l'état de l'agrégat | Dans la méthode de transition |
| Réaction à un fait métier | Handler d'event (skill `nouveau-domain-event-handler`) |
| Coordination entre agrégats | Service de domaine — jamais dans l'agrégat |

## Exemple de référence

`backend/LoreBank.Bank.Domain/Aggregates/BankAccount.cs`, avec ses events,
exceptions et id typé dans le même projet.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Constructeurs privés, `sealed` (agrégat, events, exceptions) | `DomainConventionTest` (SharedKernel.Test.Infrastructure) |
| Namespace d'exception nommant le module — le préfixe du code en dépend | `ModuleCompositionTest` |
| Codes d'erreur publiés | `ExceptionCodesTest` du module — étape 9 |
| Invariants et ordre des events | les tests de transition — étape 9 |
| Le Domain ne lit jamais l'horloge (ADR 0024) | le build : l'analyseur d'API bannies (`backend/BannedSymbols.txt`) rougit en `RS0030` sur `DateTime.Now`/`UtcNow` et `DateTimeOffset.Now`/`UtcNow` dans tout projet `.Domain` ou `.Application` |
| Version d'agrégat (ADR 0020) : rien à déclarer, le socle pose le jeton de concurrence sur tout `AggregateRoot` et refuse une écriture périmée en 409 | `ModuleDbContextTest`, `ModuleRepositoryTest` (socle) ; `ConcurrentUpdateTest` (module de référence) |
| Un jumeau publié part vraiment dans l'outbox, avec sa ressource de Signal s'il signale (ADR 0014, 0026) | `IntegrationEventPublicationTest` et `SignalPublicationTest` du module (sur `OutboxProbe` et `SignalProbe`) ; `ModuleCompositionTest` — un `ISignalsClients` sans `[IntegrationEvent]` |

## Pièges

- L'event décrit un fait accompli : muter l'état, **puis** émettre.
- Un event se nomme au passé (`BankAccountOpenedDomainEvent`) — l'impératif
  (`OpenAccount`) décrit une intention, pas un fait. Le suffixe `DomainEvent`
  le distingue de son éventuel jumeau publié (`…IntegrationEvent`).
- L'agrégat porte l'état qui décide, pas celui qui s'affiche — une donnée de
  lecture pure vient d'un reader, elle n'a rien à faire ici.
- La réaction à un event vit dans un handler, jamais dans l'agrégat émetteur.

## Avant de terminer

Build sans warning, et les tests de l'étape 9 verts : cycle nominal, chaque
invariant rejeté avec son exception, ordre des events accumulés vérifié,
codes épinglés.
