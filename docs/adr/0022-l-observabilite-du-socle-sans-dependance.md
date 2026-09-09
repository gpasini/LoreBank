# L'observabilité du socle sans dépendance

> Statut : accepté — 2026-09-09.

L'hôte n'avait ni santé, ni corrélation visible dans ses logs, ni mesure de
ses outbox : les lignes poison étaient gardées « pour un humain » (ADR 0014)
sans qu'aucune surface ne les compte. On décide que le socle porte ce qui ne
demande aucun choix de fournisseur, et rien d'autre. **Santé** : deux
endpoints hors `api/` et hors Description, `/health/live` (Vivacité : le
process répond) et `/health/ready` (Disponibilité : chaque base de module
joignable), servis par un seul check du socle qui parcourt les
`IHostModule` du conteneur à l'exécution et rapporte chaque module dans la
réponse — le harnais voit ainsi son ProbeModule, le socle reste prouvé sur
son terrain (ADR 0017). Les outbox n'y entrent pas : une ligne en attente ou
poison n'est pas une indisponibilité. **Corrélation** : l'identifiant de
trace W3C que le framework pose déjà sur chaque requête et propage depuis un
`traceparent` entrant, rendu visible par la console JSON du framework avec
ses scopes, et porté par `ApiProblem` en extension `traceId` sur toutes les
erreurs — un 500 sans identifiant n'est pas diagnosticable. **Mesure des
outbox** : deux jauges `System.Diagnostics.Metrics` par module,
`lorebank.outbox.pending` et `lorebank.outbox.poisoned`, rafraîchies à
chaque passe du processor, et un log de synthèse à la cadence de purge quand
un module a des lignes poison. L'export des traces et métriques
(OpenTelemetry) et l'image conteneur sont des chantiers séparés, avec leur
ADR : ils engagent un fournisseur, le socle non.

## Options écartées

- **Serilog** : le réflexe, et c'est pour ça qu'il sera proposé. Écarté
  parce que la console JSON de `Microsoft.Extensions.Logging` écrit déjà
  scopes et `TraceId`, que tout collecteur lit du JSON sur stdout, et qu'une
  dépendance de logging dans le socle est un choix que chaque cloneur
  hériterait sans l'avoir fait.
- **OpenTelemetry dans le socle** : les jauges `System.Diagnostics.Metrics`
  sont ce qu'OpenTelemetry exporte ; les poser sans lui laisse le cloneur
  libre de l'exporteur, et `dotnet-counters` les lit dès aujourd'hui.
- **Un check par module enregistré à la composition** : ne verrait pas un
  module monté après (le ProbeModule du harnais), et le test de
  Disponibilité s'ancrerait sur les modules d'exemple.
- **Un seul `/health`** : forcerait à confondre « tuer le process » et
  « retirer du trafic », deux questions que l'orchestrateur pose séparément.
- **Un endpoint qui liste les lignes poison** (payload, dernière erreur) et
  leur réactivation : attendent l'authentification — sans elle, la surface
  serait publique.

## Conséquences

- `ApiProblem` gagne `traceId` : `ErrorContractTest` l'épingle, le schéma de
  la Description change, le Client du front se régénère.
- Un comptage par module et par passe du processor, bon marché tant que la
  Rétention (ADR 0021) tient les tables petites.
- La preuve du 503 se fait sur le check en isolation, contre une base
  injoignable — le conteneur partagé du harnais ne se coupe pas sans casser
  les fixtures voisines.
