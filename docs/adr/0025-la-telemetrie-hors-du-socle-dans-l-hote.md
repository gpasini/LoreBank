# La Télémétrie hors du socle, dans l'hôte

> Statut : accepté — 2026-09-09.

L'ADR 0022 a posé la Corrélation et la mesure des outbox en primitives de la
BCL (`Activity`, `Meter`) et renvoyé leur export à un chantier séparé : le
socle porte ce qui n'engage aucun fournisseur. OpenTelemetry est ce
fournisseur — OTLP est un standard sans propriétaire, mais ses packages sont
des dépendances que chaque cloneur hériterait. On décide la même géométrie
que pour l'Acteur (ADR 0023) : **le socle fournit la primitive neutre,
l'hôte branche le fournisseur**. `LoreBank.Host` seul référence
OpenTelemetry (hébergement, instrumentation ASP.NET Core, exporteur OTLP) et
compose la Télémétrie dans un bloc identifiable de sa composition ;
`SharedKernel.*` et les modules restent sans référence — un cloneur qui n'en
veut pas retire un bloc et trois pins. Le socle gagne en BCL ce qui manquait
pour que l'export ait quelque chose à montrer côté outbox : une `Activity`
par ligne traitée et par handler consommateur (source « LoreBank.Outbox »,
nom `process <discriminant>`), **enfant de la trace de la commande
d'origine** — la ligne d'outbox garde le `traceparent` courant au moment de
son écriture, et le chemin Bank → Ledger se lit d'un bloc dans une seule
trace. Traces et métriques sortent ; les logs restent sur la sortie
standard (ADR 0022), la Corrélation fait le pont. Le SDK est toujours monté,
l'exporteur seulement quand `OTEL_EXPORTER_OTLP_ENDPOINT` est défini : sans
endpoint, rien ne sort, rien n'est tenté, rien n'est loggé — et le harnais
branche un exporteur en mémoire par les hooks du SDK pour prouver le
câblage sans réseau. Les clés sont les standard `OTEL_*`, lues par le SDK
lui-même via la configuration de l'hôte : aucune section maison.

## Options écartées

- **L'export dans le socle** (`AddTelemetryExport` à côté
  d'`AddOpenApiDescription`) : cohérent avec la Description, mais le socle
  référencerait OpenTelemetry — exactement ce que l'ADR 0022 refusait, et
  un choix que chaque cloneur hériterait sans l'avoir fait.
- **Documenté, pas câblé** : zéro dépendance, mais le cloneur recâble tout
  et rien ne prouve que les jauges et la Corrélation s'exportent vraiment.
- **Un lien plutôt qu'un parent** entre l'activité du consommateur et la
  trace d'origine : la recommandation des conventions de messagerie pour
  l'asynchrone, mais les dashboards affichent mal les liens, et le bénéfice
  du template est de montrer le chemin inter-modules entier.
- **Une activité par passe du processor** : un span vide par seconde et par
  module ; le fait d'intérêt est la livraison d'un event, pas le tick.
- **Rien monté sans endpoint** : plus pur, mais intestable — on ne retire
  pas un exporteur OTLP d'un pipeline déjà composé, et un endpoint bidon
  parlerait réseau en CI.
- **Les logs en OTLP** : trois lignes que le cloneur ajoute s'il en veut ;
  deux chemins de logs à ne pas doubler en prod, pour rien dans le template.
- **Les packages d'instrumentation Npgsql, HttpClient et runtime** : Npgsql
  et HttpClient exposent nativement leur `ActivitySource` et leur `Meter`,
  un `AddSource`/`AddMeter` suffit ; le runtime est hors du chantier.
  L'instrumentation ASP.NET Core est gardée : en .NET 10, le span serveur
  natif n'a ni statut ni route.

## Conséquences

- Une colonne `trace_parent` sur `__outbox`, posée par le DDL idempotent
  des tables d'integration events ; nulle pour une ligne écrite hors
  activité (migration de données, test), sans erreur.
- Le tick de l'outbox fait du SQL hors de toute trace : sans activité par
  passe, ces spans Npgsql seraient des traces racines, une par seconde et
  par module. L'hôte les filtre — un span SQL sans parent n'est pas
  exporté ; ce qui sort est toujours sous une requête ou une livraison.
- Une source oubliée dans la composition n'échoue nulle part : c'est le
  test du câblage, sur l'exporteur en mémoire et le ProbeModule, qui
  l'épingle.
- Le poste de dev exporte par défaut vers Jaeger et Prometheus du
  `docker-compose.yml`, en `http/protobuf`, l'endpoint des métriques
  redirigé par la clé par signal ; sans les conteneurs, l'exporteur échoue
  hors `ILogger`, sans gêner.
- Le nom de service par défaut, « LoreBank », est une chaîne de plus que le
  renommage du template devra toucher.
