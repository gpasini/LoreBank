---
name: nouveau-value-object
description: À utiliser avant de créer ou modifier un value object du domaine — donnée typée sans identité (IBAN, montant, référence, code) — ou quand un primitif (string, decimal, Guid) circule nu dans le domaine avec sa validation éparpillée.
---

# Nouveau value object

## Principe

Un value object est défini par ses valeurs, immuable, et **invalide ne peut
pas être créé** : il ne s'instancie que par ses factories statiques — la
**création** nommée, qui normalise puis valide, et **`Hydrate`**, qui truste
la base (ADR 0016 : la validation vit aux frontières, la réhydratation
re-représente un fait établi — relire n'est pas re-décider). Le constructeur
est privé. Les VO du socle sont des **classes, pas des records** (choix
délibéré) : l'égalité vient de `ValueObject`.

## Choisir la base

| Cas | Base | Exemple |
|---|---|---|
| Une seule valeur enveloppée | `SimpleValueObject<T>` (ctor `protected`) | `Iban`, `BankAccountId` |
| Plusieurs champs | `ValueObject` + `GetEqualityComponents()` | `Money` |
| Restreindre un VO existant | envelopper (propriété `Value`), déléguer sa validation | `PositiveMoney` |

## Recette

1. Classe `sealed` dans `ValueObjects/` du Domain concerné —
   `SharedKernel.Domain` seulement si plusieurs modules la partagent.
   Constructeur **privé**, brut : il assigne, rien d'autre.
2. La **factory de création**, nommée métier (`Iban.Parse`, `Money.Of`,
   `XxxId.New`, `LedgerAccountRef.ForBankAccount`) : **normaliser d'abord**
   (espaces, casse), **valider ensuite**, lever l'exception dédiée sinon.
   Une valeur correcte par construction (`ForBankAccount` fabrique la forme
   canonique) ne revalide rien.
3. **`Hydrate`** si — et seulement si — une conversion EF le consomme : il
   passe la valeur stockée au constructeur brut, sans normaliser ni valider.
   Pas de `Hydrate` sans appelant (`PositiveMoney`, jamais persisté tel quel,
   n'en a pas). Un VO multi-champs owned (`Money`) n'en a pas non plus : EF
   lie son constructeur privé brut par noms de paramètres.
4. Propriétés `get`-only ; une opération retourne une **nouvelle instance**
   (`Money.Add`, via le ctor brut — l'invariant est déjà prouvé), et son
   invariant propre lève une exception dédiée (`CurrencyMismatchException`).
5. L'exception : `sealed`, héritant de `DomainException`, dans `Exceptions/`.
   Elle ne porte aucun texte : elle passe à sa base un dictionnaire de
   **primitives** à clés camelCase (`["currency"] = currency`) — jamais un VO,
   le front reçoit le code dérivé du type (`INVALID_IBAN`) et internationalise.
6. Regex de validation via `[GeneratedRegex]` (classe `partial`).
7. Le test unitaire du VO (`ValueObjects/XxxTest.cs`) : égalité par valeur,
   normalisation, chaque règle de validation rejetée avec son exception —
   sur la factory de création — et le cas « `Hydrate` accepte tel quel » si
   un `Hydrate` existe.

## Exemples de référence

- `backend/LoreBank.SharedKernel.Domain/ValueObjects/Iban.cs` — mono-valeur,
  `Parse` (normalisation + regex) / `Hydrate`.
- `Money.cs` — multi-champs, `Of`, ctor brut lié par EF (owned), opérations.
- `PositiveMoney.cs` — restriction par enveloppement, validation déléguée,
  pas de `Hydrate`.
- `LedgerAccountRef.cs` (Ledger) — factories correctes par construction
  (`Cash`, `ForBankAccount`) + `Parse` gardien de la forme canonique +
  `Hydrate`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Aucun constructeur public sur un VO | `DomainConventionTest` (SharedKernel.Test.Infrastructure) |
| `sealed`, propriétés get-only | `DomainConventionTest` |
| Code d'erreur dérivé du type, préfixé du module par le namespace | `DomainExceptionTest`, `ExceptionCodesTest` du module, `ModuleCompositionTest` |
| Invalide ne peut pas être créé | le test unitaire du VO — c'est l'étape 7, pas une option |

## Pièges

- `Hydrate` ne se consomme **jamais depuis du code métier** : c'est le geste
  des conversions EF. Un handler qui hydrate contourne la validation des
  frontières.
- Normaliser **avant** de valider — sinon `"fr76 …"` est rejeté à tort.
- Garder le typage de bout en bout : un `implicit operator` vers le primitif
  l'annule.
- Faire confiance au VO en aval : l'agrégat ne revérifie pas ce que la
  factory de création garantit.
- L'exception hérite de `DomainException` — une `ArgumentException` sortirait
  en 500 anonyme au lieu d'un 422 codé.
- Un `Money` owned n'a droit qu'à **un** propriétaire EF : deux propriétés
  bâties sur la même instance font partir la seconde en NULL — copier
  (`Money.Of`) plutôt que partager (voir `JournalLine.Of`).

## Avant de terminer

Build sans warning, et les tests de l'étape 7 verts. Si le VO vit dans un
module, son exception a sa ligne dans l'`ExceptionCodesTest` du module — le
code est un contrat public, le test épingle le renommage.
