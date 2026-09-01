---
name: nouvel-agregat
description: À utiliser avant de créer ou faire évoluer un agrégat du domaine — nouveau concept métier avec identité et cycle de vie (compte, virement, dossier…), ajout d'une transition d'état, ou déplacement de logique métier vers le domaine.
---

# Nouvel agrégat

## Principe

L'agrégat est la frontière de cohérence : constructeur privé, factory statique,
invariants dans les méthodes de transition, un domain event par transition.
Impossible d'obtenir un agrégat dans un état invalide.

## Recette

1. **Id typé** : `sealed class XxxId(Guid value) : SimpleValueObject<Guid>` avec
   factory `New()`, dans `Aggregates/`.
2. **Classe** `sealed class Xxx : AggregateRoot<XxxId>`, constructeur `private`.
3. **Naissance** : factory statique (`Open`, `Create`…) qui construit puis émet
   l'event de naissance via `AddDomainEvent`.
4. **Transitions** : une méthode par opération métier (`Deposit`, `Close`…) qui
   (a) vérifie les gardes de cycle de vie, (b) vérifie ses invariants et lève
   une exception dédiée sinon, (c) mute l'état (`private set`), (d) émet son
   event.
5. **Events** : `sealed record` au passé implémentant `IDomainEvent`, dans
   `Events/`, portant l'id de l'agrégat et les données utiles au consommateur.
6. **Exceptions** : une classe `sealed : DomainException` par invariant violé,
   dans `Exceptions/`, message porteur des valeurs.
7. **Gardes récurrentes** : factoriser en méthode privée (`EnsureIsOpen`).

## Répartition des invariants

| Invariant | Où |
|---|---|
| Format/cohérence d'une valeur | Dans le VO (le construire suffit) |
| Règle sur l'état de l'agrégat | Dans la méthode de transition |
| Coordination entre agrégats | Service de domaine — pas dans l'agrégat |

Ne pas revérifier dans l'agrégat ce qu'un VO garantit (ex. le mismatch de
devise est levé par `Money`, pas par le compte).

## Exemple de référence

`backend/LoreBank.Bank.Domain/Aggregates/BankAccount.cs` et ses events,
exceptions et id typé dans le même projet.

## Pièges

- Constructeur public ou setters publics : l'état devient pilotable de
  l'extérieur.
- Event nommé à l'impératif (`OpenAccount`) : un event est un fait passé
  (`BankAccountOpened`).
- Émettre l'event avant que l'état soit muté — l'event décrit un fait accompli.
- Grossir l'agrégat avec des données servant uniquement à l'affichage.

## Avant de terminer

Build sans warning + démonstration à l'exécution : cycle nominal, chaque
invariant rejeté avec son exception, ordre des events accumulés.
