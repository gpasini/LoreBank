---
name: nouveau-value-object
description: À utiliser avant de créer ou modifier un value object du domaine — donnée typée sans identité (IBAN, montant, référence, code) — ou quand un primitif (string, decimal, Guid) circule nu dans le domaine avec sa validation éparpillée.
---

# Nouveau value object

## Principe

Un value object est défini par ses valeurs, immuable, et **invalide ne peut pas
exister** : normalisation puis validation dans le constructeur, rien à
revérifier en aval. Les VO du socle sont des **classes, pas des records**
(choix délibéré) : l'égalité vient de `ValueObject`.

## Choisir la base

| Cas | Base | Exemple |
|---|---|---|
| Une seule valeur enveloppée | `SimpleValueObject<T>` | `Iban`, `BankAccountId` |
| Plusieurs champs | `ValueObject` + `GetEqualityComponents()` | `Money` |
| Restreindre un VO existant | envelopper (propriété `Value`), déléguer sa validation | `PositiveMoney` |

## Recette

1. Classe `sealed` dans `ValueObjects/` du Domain concerné —
   `SharedKernel.Domain` seulement si plusieurs modules la partagent.
2. Constructeur : **normaliser d'abord** (espaces, casse), **valider ensuite**,
   lever l'exception dédiée sinon.
3. Propriétés `get`-only ; une opération retourne une **nouvelle instance**
   (`Money.Add`), et son invariant lève une exception dédiée
   (`CurrencyMismatchException`).
4. L'exception : `sealed`, héritant de `DomainException`, dans `Exceptions/`.
   Elle ne porte aucun texte : elle passe à sa base un dictionnaire de
   **primitives** à clés camelCase (`["currency"] = currency`) — jamais un VO,
   le front reçoit le code dérivé du type (`INVALID_IBAN`) et internationalise.
5. Regex de validation via `[GeneratedRegex]` (classe `partial`).
6. Le test unitaire du VO (`ValueObjects/XxxTest.cs`) : égalité par valeur,
   normalisation, chaque règle de validation rejetée avec son exception.

## Exemples de référence

- `backend/LoreBank.SharedKernel.Domain/ValueObjects/Iban.cs` — mono-valeur,
  normalisation + regex.
- `Money.cs` — multi-champs, opérations, invariant de devise.
- `PositiveMoney.cs` — restriction par enveloppement, validation déléguée.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| `sealed`, propriétés get-only | `DomainConventionTest` (SharedKernel.Test.Infrastructure) |
| Code d'erreur dérivé du type, préfixé du module par le namespace | `DomainExceptionTest`, `ExceptionCodesTest` du module, `ModuleCompositionTest` |
| Invalide ne peut pas exister | le test unitaire du VO — c'est l'étape 6, pas une option |

## Pièges

- Normaliser **avant** de valider — sinon `"fr76 …"` est rejeté à tort.
- Garder le typage de bout en bout : un `implicit operator` vers le primitif
  l'annule.
- Faire confiance au VO en aval : l'agrégat ne revérifie pas ce que le
  constructeur garantit.
- L'exception hérite de `DomainException` — une `ArgumentException` sortirait
  en 500 anonyme au lieu d'un 422 codé.

## Avant de terminer

Build sans warning, et le test de l'étape 6 vert. Si le VO vit dans un module,
son exception a sa ligne dans l'`ExceptionCodesTest` du module — le code est
un contrat public, le test épingle le renommage.
