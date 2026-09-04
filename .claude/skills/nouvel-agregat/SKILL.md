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
6. **Events** : `sealed record` nommé au passé (`BankAccountOpened`),
   implémentant `IDomainEvent`, dans `Events/`, portant l'id de l'agrégat et
   les données utiles au consommateur.
7. **Exceptions** : une classe `sealed : DomainException` par invariant, dans
   `Exceptions/`. Le constructeur peut prendre des VO, mais le dictionnaire
   passé à la base ne porte que des **primitives** à clés camelCase
   (`["balance"] = balance.Amount`) — jamais de texte.
8. **Tests** : une classe par méthode (`Domain/<Agrégat>/<Méthode>Test.cs`) —
   cycle nominal, chaque invariant rejeté avec son exception, events émis dans
   l'ordre — et une ligne par exception dans l'`ExceptionCodesTest` du module.

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
| Codes d'erreur publiés | `ExceptionCodesTest` du module — étape 8 |
| Invariants et ordre des events | les tests de transition — étape 8 |

## Pièges

- L'event décrit un fait accompli : muter l'état, **puis** émettre.
- Un event se nomme au passé (`BankAccountOpened`) — l'impératif
  (`OpenAccount`) décrit une intention, pas un fait.
- L'agrégat porte l'état qui décide, pas celui qui s'affiche — une donnée de
  lecture pure vient d'un reader, elle n'a rien à faire ici.
- La réaction à un event vit dans un handler, jamais dans l'agrégat émetteur.

## Avant de terminer

Build sans warning, et les tests de l'étape 8 verts : cycle nominal, chaque
invariant rejeté avec son exception, ordre des events accumulés vérifié,
codes épinglés.
