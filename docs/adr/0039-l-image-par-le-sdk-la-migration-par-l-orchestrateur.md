# L'image par le SDK, la migration par l'orchestrateur

> Statut : accepté — 2026-09-14. Prolonge l'ADR 0006 jusqu'au déploiement.

L'hôte savait migrer hors de son démarrage (ADR 0006 : le verbe `migrate`,
`mise run migrate`) et servir, mais rien ne disait comment il se déployait.
Pas de Dockerfile, un `docker-compose.yml` qui ne porte que PostgreSQL et la
télémétrie du poste, et une question sans réponse : qui enchaîne la migration
et le service, quand deux réplicas démarrent en même temps ? La cible est un
orchestrateur — Kubernetes à terme — qui tire une image d'un registre et
sonde sa santé en HTTP (ADR 0022). Il fallait une image, et l'enchaînement
écrit quelque part de rejouable.

## La décision

**L'image est construite par le SDK**, sans Dockerfile :
`dotnet publish LoreBank.Host /t:PublishContainer`, derrière
`mise run //backend:image`. Les propriétés `Container*` de
`LoreBank.Host.csproj` disent le reste : base `aspnet:10.0-alpine`, nom
`lorebank`, tag `latest`, deux RID musl (`linux-musl-x64`,
`linux-musl-arm64`) en un seul publish, donc un manifeste multi-arch — le
poste prend sa native, le cluster la sienne. Le registre et le push sont au
cloneur : `ContainerRegistry` suffit. La toolchain reste déclarée une fois,
dans mise et `global.json` ; un Dockerfile aurait été une seconde version
de SDK à tenir en phase.

**La même image migre et sert, et c'est l'orchestration qui enchaîne.**
L'entrypoint ne migre jamais (ADR 0006) : `docker-compose.deploy.yml` le
dit en trois services — `postgres` avec son `pg_isready`, `migrate` sur
l'image avec `command: migrate` après un PostgreSQL sain, `api` après un
`migrate` terminé avec succès, scalable. Sous Kubernetes, c'est un Job
avant le rollout du Deployment ; `docs/deploiement.md` le traduit. Pas de
healthcheck sur `api` dans le compose : l'image n'embarque pas `curl`, et
`/health/live` et `/health/ready` sont faits pour les sondes HTTP de
l'orchestrateur.

**Le déploiement est une Porte** (ADR 0028) : `mise run //:deploy:smoke`
construit l'image, monte le compose avec deux réplicas d'`api`, lit
`/health/ready` sur chacun, vérifie dans les logs que seul `migrate` a
touché au schéma, et démonte tout, volumes compris. Sur le poste comme en
CI, le même compose qu'un cloneur déploierait.

Ce qui ne change pas : le front (ADR 0036) reste hors de l'image — statique,
servi à part, sous la même origine par l'Ingress du cloneur.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| La Porte `//:deploy:smoke` retirée de `check` ou sans step CI | `QualityGateFreezeTest` (le Gel, ADR 0031) |
| Un réplica d'`api` migre au démarrage | `deploy:smoke` : la catégorie EF des migrations dans les logs d'`api` |
| L'image ne démarre pas, ne sert pas, ou `migrate` ne sort pas en succès | `deploy:smoke` : `up --wait` et `/health/ready` |
| L'image ne se construit plus (base image, RID, propriété retirée) | `deploy:smoke`, par sa dépendance `//backend:image` |

## Options écartées

- **Un Dockerfile multi-stage** : universel, lisible par tout ops et toute
  plateforme qui construit depuis un Dockerfile. Écarté parce que la cible
  ne construit rien — un orchestrateur tire une image — et parce que le
  `FROM sdk:` serait une seconde déclaration de version, à côté de
  `global.json`, que la CI rejouerait en tirant l'image SDK pour recompiler
  ce que mise a déjà compilé. Le jour où une plateforme l'exige, il s'écrit
  en dix lignes : `FROM aspnet:10.0-alpine`, `COPY` du publish.
- **Migrer dans l'entrypoint**, avec un verrou : chaque réplica migrerait au
  boot, et le verrou déplacerait le problème dans la base. L'ADR 0006 l'a
  déjà écarté ; ici on écrit seulement qui enchaîne à sa place.
- **Un verbe `health` dans l'hôte** pour donner un `HEALTHCHECK` au compose
  sans `curl` : une surface de plus dans le socle pour un seul consommateur,
  quand l'orchestrateur cible sonde en HTTP.
- **Des manifestes Kubernetes ou un chart Helm** dans le template : les
  conventions d'un cluster varient trop ; un paragraphe qui nomme Job,
  Deployment et sondes suffit, et ne s'entretient pas.
- **Le front dans l'image** (`wwwroot`, fallback SPA) : le socle apprendrait
  à servir des fichiers pour une décision que l'ADR 0036 laisse au
  déploiement.
- **Une architecture seule** (`linux-x64`) : le smoke tournerait en émulation
  sur un poste arm64. Le multi-arch coûte une propriété.

## Le coût assumé

La Porte tire deux images de base Alpine au premier passage, construit deux
publish et monte trois conteneurs : de l'ordre d'une minute à chaud sur le
poste, un peu plus sur le runner. Et `check` exige désormais Docker Compose
et `curl` en plus du démon — que Testcontainers exigeait déjà. Ni l'un ni
l'autre n'est épinglé par mise : Compose vient avec Docker, `curl` avec
macOS et Ubuntu ; un poste qui n'a pas l'un des deux rougit au `up` ou au
`GET`, et `docs/deploiement.md` le dit.
