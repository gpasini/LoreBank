# Déployer : l'image, la migration, le service

L'hôte se déploie en une image OCI qui sait deux choses : servir du HTTP, et
migrer puis sortir. C'est l'orchestration qui enchaîne l'une avant l'autre,
jamais l'entrypoint. Décision et alternatives : ADR 0039 ; pourquoi l'API ne
migre jamais au démarrage : ADR 0006.

## L'image

```bash
mise run //backend:image      # lorebank:latest dans le démon Docker local
```

Le SDK .NET construit l'image (`dotnet publish /t:PublishContainer`), sans
Dockerfile ni `docker build`. Les propriétés `Container*` de
`backend/LoreBank.Host/LoreBank.Host.csproj` la décrivent : base
`mcr.microsoft.com/dotnet/aspnet:10.0-alpine`, et l'architecture du poste,
inférée par le SDK avec le musl de la base — une image native pour le
compose. Le reste est le défaut du SDK, absent du csproj : utilisateur
non-root `app`, port 8080, entrypoint `dotnet LoreBank.Host.dll`.

Pour pousser dans un registre, une propriété de plus, et le `docker login`
du poste ou du runner fait le reste. Avec un registre, le publish produit
les deux architectures (`linux-musl-x64`, `linux-musl-arm64`) et pousse un
index multi-arch — jamais en local : un démon Docker sans store containerd
refuse un index (`CONTAINER1020`).

```bash
cd backend && mise exec -- dotnet publish LoreBank.Host -c Release /t:PublishContainer \
  -p:ContainerRegistry=ghcr.io/mon-org -p:ContainerImageTag=$(git rev-parse --short HEAD)
```

## Le compose de déploiement

`docker-compose.deploy.yml` est la référence exécutable ; `docker-compose.yml`
reste celui du poste (PostgreSQL, Jaeger, Prometheus).

```bash
docker compose -f docker-compose.deploy.yml up --wait --scale api=2
```

Trois services : `postgres` avec son healthcheck `pg_isready` ; `migrate`,
la même image avec `command: migrate`, qui attend un PostgreSQL sain, migre
tous les modules et sort ; `api`, qui ne démarre qu'après un `migrate`
terminé avec succès et se scale librement — l'outbox tient plusieurs
instances (ADR 0021). L'image n'est jamais tirée (`pull_policy: never`) :
un cloneur qui déploie depuis un registre pose `LOREBANK_IMAGE` et retire
cette ligne.

## La configuration

Tout passe par l'environnement, comme le hosting .NET le lit :

| Variable | Rôle |
|---|---|
| `ConnectionStrings__BankDb`, `ConnectionStrings__LedgerDb` | une par module, le nom est celui de `appsettings.json` |
| `ASPNETCORE_ENVIRONMENT` | `Production` par défaut dans l'image : logs JSON, ni `/scalar` ni `/openapi` |
| `ASPNETCORE_HTTP_PORTS` | `8080` par défaut dans l'image |
| `OTEL_EXPORTER_OTLP_ENDPOINT` et compagnie | la Télémétrie (`docs/telemetrie.md`) : rien n'est exporté sans endpoint |

## Sous Kubernetes

Le même enchaînement, avec les objets du cluster :

- un **Job** `migrate` — l'image, `args: ["migrate"]`, `restartPolicy:
  Never` — lancé avant chaque rollout : hook Helm `pre-upgrade`, sync wave
  ArgoCD, ou un step de pipeline qui attend sa fin ;
- un **Deployment** `api` — la même image, sans args — avec
  `livenessProbe` sur `/health/live` et `readinessProbe` sur
  `/health/ready` (ADR 0022) ; les réplicas se partagent l'outbox ;
- le front (ADR 0036) dans une image statique à part, et un Ingress qui
  route `/` vers lui et `/api` vers `api` : la même origine, sans CORS.

Le template n'embarque ni manifestes ni chart : les conventions d'un
cluster sont les siennes.

## La Porte

`mise run //:deploy:smoke` — dans `mise run check` et en CI (ADR 0028) —
construit l'image, monte le compose avec deux réplicas, lit
`/health/ready` sur chacun (ports 8080 et 8081), vérifie dans les logs que
seul `migrate` a touché au schéma, puis démonte tout, volumes compris.

## Pièges

- **Docker Compose et `curl`** : la Porte les appelle et mise ne les épingle
  pas — Compose vient avec Docker, `curl` avec macOS et Ubuntu. Sans l'un,
  elle rougit au `up` ou au `GET`.
- **`CONTAINER1020` au publish** : une liste de RID est arrivée jusqu'au
  démon local. Le multi-arch ne s'active qu'avec `ContainerRegistry` ; sans
  registre, aucune propriété `RuntimeIdentifier*` ne doit être posée.
- **Ports 8080 et 8081 occupés** sur le poste : la Porte échoue au `up` ;
  libérer, ou changer la plage dans le compose.
- **Le premier passage est long** : deux images de base à tirer, deux
  publish. Les suivants tiennent en une minute à chaud.
- **`ContainerRuntimeIdentifiers` sans `RuntimeIdentifiers`** : le restore
  ne connaît pas les RID et le publish s'arrête sur NETSDK1047. Les deux
  propriétés vont ensemble, sous la même condition.
