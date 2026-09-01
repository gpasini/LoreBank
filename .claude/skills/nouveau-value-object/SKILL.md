---
name: nouveau-value-object
description: À utiliser avant de créer ou modifier un value object (VO) du domaine — donnée typée sans identité comme un IBAN, un montant, une référence, un code — ou quand un primitif (string, decimal, Guid) circule nu dans le domaine avec sa validation éparpillée.
---

# Nouveau value object

## Principe

Un value object est défini par ses valeurs, immuable, et **invalide ne peut pas
exister** : toute la normalisation et la validation vivent dans le constructeur.

Ici les VO sont des **classes, pas des records** (choix délibéré du socle :
l'égalité vient de `ValueObject`, et les records ont été écartés).

## Choisir la classe de base

| Cas | Base | Exemple |
|---|---|---|
| Une seule valeur enveloppée | `SimpleValueObject<T>` | `Iban`, `BankAccountId` |
| Plusieurs champs | `ValueObject` + `GetEqualityComponents()` | `Money` |

## Recette

1. Classe `sealed` dans `ValueObjects/` du projet Domain concerné
   (`SharedKernel.Domain` seulement si plusieurs modules le partagent).
2. Constructeur : **normaliser d'abord** (espaces, casse), **valider ensuite**,
   lever une exception dédiée sinon.
3. Propriétés `get`-only. Pas de setter, pas d'`init`.
4. Une exception `sealed` héritant de `DomainException`, dans `Exceptions/`,
   dont le message contient la valeur fautive.
5. Regex de validation via `[GeneratedRegex]` (classe `partial`).
6. Les opérations qui retournent un VO retournent une **nouvelle instance**
   (cf. `Money.Add`) ; un invariant d'opération lève une exception dédiée
   (cf. `CurrencyMismatchException`).

## Exemple de référence

`backend/LoreBank.SharedKernel.Domain/ValueObjects/Iban.cs` (mono-valeur,
normalisation + regex) et `Money.cs` (multi-champs, opérations, invariant de
devise).

## Pièges

- Valider avant de normaliser — `"fr76 …"` serait rejeté à tort.
- Exposer un `implicit operator` vers le primitif : il annule le typage.
- Revalider dans l'agrégat ce que le VO garantit déjà.
- Oublier l'exception dédiée et lever `ArgumentException` : les erreurs métier
  héritent de `DomainException`.

## Avant de terminer

Build sans warning + comportement démontré à l'exécution : égalité par valeur,
normalisation, chaque rejet avec son exception.
