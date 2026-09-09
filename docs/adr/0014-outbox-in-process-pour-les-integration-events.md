# L'outbox in-process pour les integration events

> Statut : accepté — 2026-09-05.

Un fait métier qui doit sortir de son module — un integration event — part par
une **outbox** : un domain event handler du module publieur le mappe et le
confie à `IIntegrationEventPublisher` (`SharedKernel.Contracts`), dont
l'implémentation écrit une ligne dans `<schéma>.__outbox` par la connexion du
DbContext du module — appelée dans la transaction de la commande (le dispatch
des domain events y a lieu), la ligne part avec elle ou pas du tout. La
publication est **opt-in** : seuls les faits qu'un handler mappe sortent, le
langage interne ne fuit pas par défaut. Le discriminant stocké est un nom
stable choisi (`bank.money-deposited`, premier segment = module publieur —
la grille des codes d'erreur), jamais un nom de type .NET : renommer un
namespace n'est pas une migration de données.

La livraison est portée par un unique `IHostedService` de l'hôte
(`OutboxDispatcher`, même géométrie que `ModuleMigrator` : un service pour
toutes les outbox), qui cadence des passes d'`OutboxProcessor`. Chaque handler
consommateur — déclaré au conteneur par une
`IntegrationEventHandlerRegistration` (handler, event, module), découverte par
scan comme les `IDomainEventHandler<>` — s'exécute dans **son propre scope DI
et son propre `TransactionScope`** (`ReadCommitted`, celui du
`TransactionBehavior`), ligne d'**inbox** incluse : `<schéma>.__inbox` du
module consommateur, écrite par la même connexion que les écritures du
handler. Le marquage « dispatché » de l'outbox est volontairement **hors** de
ces transactions (schéma du publieur, connexion distincte — la joindre ferait
escalader en distribué) : la livraison est **at-least-once**, un crash entre
le commit d'un handler et le marquage rejoue l'event, et c'est l'inbox qui
rend le rejeu inoffensif. Un échec remet la ligne en attente avec backoff
exponentiel, puis la marque **poison** après `MaxAttempts` — elle sort de la
file mais reste en base, dernière erreur incluse, pour un humain. Pas de
garantie d'ordre entre events : une ligne en retry ne bloque pas celles
d'après.

Les deux tables sont créées paresseusement par `ModuleMigrator`, comme le
journal des data migrations : des tables du socle, pas du module — rien à
générer ni à recopier au clonage, et le démarrage de l'API ne crée toujours
rien (ADR 0006). Corollaire assumé du dispatch in-process : un event n'est
traité que si l'hôte tourne — l'outbox garantit qu'aucun fait publié ne se
perd, pas qu'il est traité dans un délai. `OutboxPublisherTest` épingle
l'atomicité et l'échec bruyant d'un discriminant sans module ;
`OutboxProcessorTest` la livraison, l'inoffensivité du rejeu, la transaction
par handler, le poison et le « livré à personne ». Côté modules d'exemple,
`IntegrationEventPublicationTest` (Bank) prouve que le publieur écrit vraiment
son jumeau dans l'outbox, et le `CqsContractTest` du Ledger joue le chemin
complet — dépôt HTTP chez Bank, passe du processor, écriture chez Ledger,
lecture HTTP — sans affirmer la ligne d'inbox : le socle se prouve sur Probe
(ADR 0017), un module prouve seulement qu'il emprunte le chemin.

Ce chemin est celui d'un hôte ; ce qui se passe quand plusieurs hôtes le
parcourent — réservation du lot à bail — et le sort des lignes livrées —
rétention et purge — sont l'objet de l'ADR 0021.

## Options écartées

- **Un broker externe** (RabbitMQ, etc.) : réaliste, mais le template
  embarquerait une dépendance d'infrastructure que la moitié des cloneurs ne
  veulent pas. L'outbox reste la même le jour où un broker arrive — seul le
  dépileur change.
- **Notification post-commit sans outbox** (MediatR après `SaveChangesAsync`) :
  simple, mais un crash entre le commit et le dispatch perd l'événement en
  silence — précisément le trou que l'outbox bouche.
- **Publier automatiquement tous les domain events** : moins de code, mais
  tout le langage interne fuirait par défaut — l'inverse d'un contrat publié.
- **Dédup par contrainte unique dans les tables métier** : l'idempotence
  deviendrait artisanale, à réinventer par handler ; l'inbox du socle la rend
  structurelle, comme `ModuleRepository` rend le dispatch des events
  non-oubliable.
- **Marquer l'outbox dans la transaction du handler** : exigerait d'enrôler
  les connexions de deux modules dans une même transaction — escalade en
  distribué, non supportée hors Windows, et une frontière de module abolie.
- **Exactly-once** : impossible sans transaction distribuée précisément ; le
  couple at-least-once + inbox donne la même garantie observable, au prix
  d'un rejeu interne borné.
