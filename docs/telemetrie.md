# La Télémétrie, de l'hôte au collecteur

Comment les traces et les métriques que le socle pose en BCL sortent de
l'hôte vers un collecteur OTLP — et pourquoi rien n'en sort tant qu'on ne le
demande pas. Décision et alternatives : ADR 0025.

## En une phrase

Le socle et les modules ne connaissent qu'`Activity` et `Meter` ; l'hôte
seul référence OpenTelemetry (`Telemetry.cs`), monte le SDK toujours, et
l'exporteur OTLP seulement si `OTEL_EXPORTER_OTLP_ENDPOINT` est défini.

## Ce qui sort

| Signal | Source | D'où |
|---|---|---|
| Span serveur d'une requête HTTP (statut, route) | `Microsoft.AspNetCore` | instrumentation ASP.NET Core |
| Span d'un appel HttpClient sortant | `System.Net.Http` | natif .NET |
| Span d'une requête SQL | `Npgsql` | natif Npgsql |
| Span d'une livraison d'outbox, par handler | `LoreBank.Outbox` | `OutboxTracing` (socle) |
| Métriques HTTP serveur et Kestrel | `Microsoft.AspNetCore.Hosting`, `…Server.Kestrel` | natif ASP.NET Core |
| Métriques HttpClient | `System.Net.Http` | natif .NET |
| Métriques du pool et des commandes Npgsql | `Npgsql` | natif Npgsql |
| Jauges d'outbox `lorebank.outbox.pending` / `poisoned` | `LoreBank.Outbox` | `OutboxMetrics` (socle) |
| Jauge des abonnés `lorebank.signals.subscribers`, compteur `lorebank.signals.delivered` | `LoreBank.Signals` | `SignalMetrics` (socle, ADR 0026) |

Un span SQL **sans parent** ne sort pas : chaque passe de l'outbox réserve
et mesure en SQL hors de toute requête et de tout handler, et sans ce filtre
(`RootSqlSpanFilter`, dans `Telemetry.cs`) chaque seconde déposerait ses
traces racines « postgresql » par module dans le collecteur. Le SQL n'est
exporté que sous une requête, une livraison, ou une activité qu'un cloneur
ouvre lui-même.

Les logs n'en font pas partie : ils restent sur la sortie standard, en JSON
avec leurs scopes (ADR 0022). La Corrélation fait le pont — le `TraceId` d'un
log est celui de la trace exportée, et le `traceId` d'une réponse d'erreur
aussi.

## La trace d'une livraison d'outbox

`process <discriminant>` (`process bank.money-deposited`), une activité par
handler consommateur, `Consumer`, avec :

| Attribut | Valeur |
|---|---|
| `lorebank.module` | le module publieur (celui de l'outbox) |
| `lorebank.consumer.module` | le module de l'inbox |
| `lorebank.handler` | le nom du type du handler |
| `messaging.message.id` | l'id de la ligne d'outbox |
| `lorebank.attempt` | la tentative, 1 pour la première |

Un handler qui lève : statut `Error`, exception enregistrée sur l'activité ;
le backoff et le marquage poison sont inchangés. La ligne d'outbox garde le
`traceparent` de la commande qui l'a écrite (colonne `trace_parent`), et
l'activité en est l'**enfant** : un dépôt HTTP chez Bank, sa livraison, et
l'écriture chez Ledger forment une seule trace. Une ligne écrite hors
activité (migration de données, test) est traitée sous une trace racine.
Aucune activité par passe ni par purge : le fait d'intérêt est la livraison,
pas le tick.

## Les clés

Ce sont les clés standard de la spécification OpenTelemetry, lues par le SDK
lui-même dans la configuration de l'hôte — variable d'environnement, ou clé
plate à la racine d'`appsettings*.json` (jamais une section maison) :

| Clé | Rôle |
|---|---|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | **l'interrupteur** : défini, l'exporteur OTLP est monté ; absent ou vide, rien ne sort, rien n'est tenté |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | `grpc` (défaut, port 4317) ou `http/protobuf` (port 4318, le chemin `/v1/traces` etc. est ajouté par le SDK) |
| `OTEL_EXPORTER_OTLP_TRACES_ENDPOINT`, `…_METRICS_ENDPOINT` | surcharge par signal, quand deux récepteurs se partagent le travail |
| `OTEL_EXPORTER_OTLP_HEADERS` | authentification du collecteur (`Authorization=Bearer …`) |
| `OTEL_SERVICE_NAME` | le nom de service ; « LoreBank » par défaut |
| `OTEL_RESOURCE_ATTRIBUTES` | `deployment.environment=prod,…` |

En production, l'opérateur pose ces variables sur le conteneur et l'hôte
exporte ; il ne les pose pas et l'hôte se tait. Les échecs d'export ne
passent jamais par `ILogger` : ils vont dans l'`EventSource`
`OpenTelemetry-Exporter-OpenTelemetryProtocol`, lisible par `dotnet-trace`
ou par le fichier `OTEL_DIAGNOSTICS.json` du SDK.

## Le poste de dev

`docker compose up -d` lance, à côté de PostgreSQL, Jaeger (traces,
<http://localhost:16686>) et Prometheus (métriques, <http://localhost:9090>,
récepteur OTLP activé). `appsettings.Development.json` exporte vers les deux
par défaut, en `http/protobuf` : l'endpoint de base sur Jaeger, les métriques
redirigées sur Prometheus par la clé par signal. Sans les conteneurs,
l'exporteur échoue en silence, hors des logs — rien ne gêne.

Dans Jaeger, service « LoreBank » : un `GET` de compte montre le span serveur
et son span Npgsql ; un dépôt montre, dans la même trace, le `POST`, puis
`process bank.money-deposited` avec l'écriture du Ledger. Dans Prometheus, les
jauges arrivent sous leur nom normalisé (`lorebank_outbox_pending`,
`lorebank_outbox_poisoned`, étiquette `module`), avec les
`http_server_request_duration_seconds_*` et `kestrel_*` d'ASP.NET Core et
les `db_client_*` de Npgsql — à la cadence d'export du SDK, une minute par
défaut.

## Ce que prouvent les tests

- `OutboxProcessorTest` et `OutboxPublisherTest` (socle, `ActivityListener`,
  sans OpenTelemetry) : le `trace_parent` stocké, l'activité par handler,
  ses attributs, son parent, son statut `Error`.
- `TelemetryContractTest` (hôte, exporteur mémoire du harnais, sur le
  ProbeModule) : span serveur avec statut et route, span Npgsql sous lui,
  Corrélation et Télémétrie dans la même trace, livraison d'outbox sous la
  trace du `POST`, jauges d'outbox et meter Npgsql collectés.
- `TelemetryCompositionTest` : seuls `LoreBank.Host` et le harnais
  référencent un package `OpenTelemetry.*` ; l'interrupteur ne s'allume
  qu'avec un endpoint non vide.

Rien ne parle réseau : la suite reste verte sans collecteur.

## Pour aller plus loin

Chacun de ces ajouts tient en une ligne de `Telemetry.cs` et un pin :

- **Métriques du runtime** (GC, threadpool, exceptions) :
  `OpenTelemetry.Instrumentation.Runtime`, `.AddRuntimeInstrumentation()`.
- **Logs en OTLP**, en plus de stdout : `builder.Logging.AddOpenTelemetry(…)`
  avec `UseOtlpExporter` — deux chemins de logs à ne pas doubler en prod.
- **Un collecteur OpenTelemetry** entre l'hôte et les backends, quand la
  topologie de prod le demande : un seul endpoint gRPC côté hôte, le
  fan-out dans le YAML du collecteur.
- **Retirer la Télémétrie** : supprimer `Telemetry.cs`, son appel dans
  `Program.cs`, les trois pins de l'hôte et les clés `OTEL_*` de
  `appsettings.Development.json` ; le socle continue de poser ses activités
  et ses jauges, que `dotnet-counters` et `dotnet-trace` lisent sans
  exporteur.
