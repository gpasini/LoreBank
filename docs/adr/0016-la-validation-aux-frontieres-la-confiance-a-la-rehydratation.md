# La validation aux frontières, la confiance à la réhydratation

Créer et réhydrater sont deux gestes différents que le constructeur unique
confondait. **Créer**, c'est faire entrer une valeur par une frontière : la
factory nommée du VO (`Iban.Parse`, `Money.Of`, `XxxId.New`,
`LedgerAccountRef.ForBankAccount`) normalise puis valide — aucune instance
invalide ne peut être créée, et les codes d'erreur (`INVALID_IBAN`…) restent
émis là. **Réhydrater**, c'est re-représenter un fait établi, déjà validé à
son entrée et maintenu par les data migrations (ADR 0013 — le même
raisonnement qui y neutralise le dispatcher d'events) : `Hydrate` passe la
valeur stockée au constructeur privé brut, sans normaliser ni valider —
relire n'est pas re-décider. Les conversions EF consomment `Hydrate` ; un VO
multi-champs owned (`Money`) se réhydrate par son constructeur privé brut,
que EF lie par noms de paramètres et que les opérations internes réutilisent.
Deux règles de sobriété : pas de `Hydrate` sans appelant (`PositiveMoney`,
jamais persisté tel quel, n'en a pas), et jamais de `Hydrate` depuis du code
métier. `DomainConventionTest` interdit tout constructeur public sur un
`ValueObject`.

Le trade-off est assumé : avant, chaque lecture revalidait chaque VO — une
donnée corrompue explosait bruyamment au premier `SELECT`, vertu diagnostique
payée en revalidation permanente de règles déjà tranchées. Désormais une base
corrompue circule en silence jusqu'à la prochaine écriture. La garantie
« une instance invalide ne peut pas exister » devient « ne peut pas être
créée » : la frontière est gardée, le stock est l'affaire des data
migrations. Les agrégats, eux, n'ont jamais revalidé à la lecture — EF écrit
leurs backing fields sans repasser par la factory de naissance, sans
revérifier d'invariant ni réémettre d'event : les invariants gardent les
*transitions*. Ce fait, jusqu'ici implicite dans la « concession EF », est le
même principe : la réhydratation re-représente. Un `Hydrate` d'agrégat serait
en revanche un mensonge : EF ne sait matérialiser que par constructeur (les
navigations ne sont pas liables du tout) — personne ne pourrait l'appeler
honnêtement.

## Options écartées

- **Valider en réhydratant** (statu quo) : la lecture explose bruyamment sur
  une donnée legacy — mais elle re-décide à chaque `SELECT` des règles déjà
  tranchées à l'écriture, et le coût tombe sur le chemin le plus fréquent.
- **Un `Hydrate` qui valide** : un simple renommage du constructeur — le
  geste redevient ambigu, le gain purement nominal.
- **Un `Hydrate` d'agrégat appelé par le repository** : exigerait des entités
  de persistance mappées à la main — renversement de « pas de classes
  d'entités de persistance », et la perte du change tracker sur lequel
  `ModuleRepository` repose (chargement, mini-unit-of-work, dispatch).
- **Un `Create` uniforme au lieu de noms métier** : `Parse`, `Of`, `New`
  disent le geste ; un `Create` générique dirait seulement « pas le
  constructeur ».
