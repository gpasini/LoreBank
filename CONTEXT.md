# LoreBank

Socle de monolithe modulaire : des modules métier montés par un hôte unique.
Le vocabulaire ci-dessous nomme les concepts du socle lui-même ; chaque module
métier construit sur lui apportera le sien.

## Language

**Module (métier)** :
Une tranche métier autonome montée par l'hôte — six projets (Domain,
Application, Infrastructure, Api, deux de test) déclarés d'un bloc.
_Avoid_ : composant, service, bounded context

**ModuleDbContext** :
Le DbContext d'un module métier. Porte le dispatch des domain events dans la
transaction de la commande — un module qui n'en dérive pas ne dispatche rien —
et le schéma PostgreSQL du module, dérivé de son nom.
_Avoid_ : DbContext de base, contexte partagé

**DomainAssembly** :
L'assembly Domain qu'un module déclare à l'hôte, celle où vivent ses handlers
de domain events.
_Avoid_ : assembly des handlers

**ModuleReader** :
La base des readers d'un module métier. Son seul geste, `Query<TRow>`, sert
les rows keyless du module et refuse un type à clé ou hors modèle — une
lecture ne peut pas matérialiser un agrégat ; un reader concret ne fournit
que son LINQ et sa projection vers le Result.
_Avoid_ : reader de base, helper SQL

**Row (de lecture)** :
Le miroir plat d'une table, réservé à la lecture : une classe de primitives
par table — jamais par query — enregistrée keyless (`HasNoKey` + `ToView`)
dans le modèle du module, requêtée par ses readers, jamais suivie ni écrite.
Son mapping de colonnes n'est vérifié que par les tests de relecture.
_Avoid_ : read model, projection, entité de lecture, DTO de persistance

**ModuleRepository** :
La base des repositories d'agrégats d'un module métier. Porte le chargement
requis (l'absence lève la NotFoundException du module), le mini-unit-of-work
et le passage par le SaveChangesAsync qui dispatche les events ; un repository
concret ne fournit que sa fabrique d'exception.
_Avoid_ : repository générique, repository de base

**Version (d'agrégat)** :
Le compteur d'écritures que tout agrégat porte, incrémenté à chaque
sauvegarde. Deux commandes qui ont lu la même version ne peuvent pas écrire
toutes les deux : la seconde est refusée. Porté par le socle, invisible du
Domain et des lectures.
_Avoid_ : jeton de concurrence, row version, xmin, ETag

**ModuleController** :
La base des controllers d'un module métier. Type le CQS du bord HTTP : une
commande → 204, une création → 201 + Location sans corps — une lecture ne
peut emprunter aucun des deux chemins.
_Avoid_ : controller de base, BaseController

**HostModule** :
La base des adapters `IHostModule`. Dérive l'identité du module — ses trois
assemblies, son nom et sa persistance par défaut — du DbContext ancré en
paramètre générique ; un adapter ne déclare plus que son `Module` Autofac.
_Avoid_ : ModuleIdentity, adapter d'hôte

**ProbeModule** :
Le module-terrain du harnais du socle : un module minimal monté uniquement
par les factories de test — jamais par l'hôte — sur lequel le socle prouve
ses propres invariants sans dépendre d'un module d'exemple supprimable.
_Avoid_ : module de test, module d'exemple, fake de module

**Contrats (de module)** :
Le langage publié d'un module métier : l'assembly `<Module>.Contracts` qui
porte ses integration events et ses ports de lecture publics — la seule
surface qu'un autre module a le droit de référencer.
_Avoid_ : API publique, shared kernel du module

**Integration event** :
Un fait métier qu'un module publie hors de ses frontières, suffixé
`IntegrationEvent` : type distinct du domain event qui l'origine, primitives
plates, vivant dans les Contrats du module publieur. Publié opt-in par un
domain event handler, livré at-least-once.
_Avoid_ : domain event publié, message, notification

**Outbox** :
La table d'un module publieur où ses integration events s'écrivent dans la
transaction de la commande, identifiés par un discriminant stable choisi
(jamais un nom de type .NET), et le store qui la porte : son DDL, ses
colonnes et les gestes qui les touchent — insérer, réserver, marquer,
échouer, mesurer, purger, relire — écrits une seule fois. Le store se
construit sur le DbContext d'un module et ne décide rien : le backoff et le
seuil de poison sont la politique du dispatcher, qui la dépile hors
transaction.
_Avoid_ : file d'attente, bus, table d'outbox (seule)

**ModuleDbContexts** :
La résolution « nom de module → DbContext » du socle, écrite une fois :
correspondance insensible à la casse (le discriminant est en minuscules,
ModuleName en Pascal), échec qui nomme le module absent et pointe
HostModules.All. Publisher et processor d'outbox sont ses appelants.
_Avoid_ : lookup de module, registre de DbContexts

**ModuleSql** :
Le geste SQL unique du socle : connexion empruntée au DbContext du module —
jamais ouverte en propre — refermée dans un finally compté par EF, clés de
paramètres nues, commande enrôlée dans la transaction EF courante. Migrations
de données et outbox/inbox sont des façades dessus — pas les readers, qui
requêtent des rows keyless.
_Avoid_ : helper ADO, SqlExecutor, copie locale du geste d'emprunt

**OutboxProbe** :
La surface d'outbox du harnais : fait passer une livraison, lit les lignes
(discriminant, payload, livré) et vide l'outbox d'un module désigné par son
DbContext — le garde-fou de publication d'un module se réduit à agir, faire
passer, affirmer, sans connaître le nom du service qui dépile. N'expose ni
retries, ni poison, ni inbox : des invariants du socle, pas d'un module.
_Avoid_ : helper SQL d'outbox, lecteur d'outbox

**DataMigrationProbe** :
La surface de rejeu des migrations de données offerte aux modules : arranger
des lignes en SQL brut, rejouer la migration, relire une colonne — et
relâcher une contrainte le temps du rejeu, rétablie dans un finally, pour le
maillon central du triptyque. Le test d'une migration n'écrit plus de
plomberie ADO ; le geste d'emprunt reste celui du socle, invisible des
assemblies de test de module.
_Avoid_ : helper SQL de test, base de test de migration

**Hydrate** :
La factory de réhydratation d'un value object : reprend la valeur stockée
telle quelle, sans normaliser ni valider — la validation vit dans les
factories de création (`Parse`, `Of`, `New`…), la base est trustée. Réservé
aux conversions EF pour un VO porteur de règles ; un id sans invariant
réhydrate aussi l'id reçu du fil — il n'y a rien à re-décider.
_Avoid_ : constructeur de lecture, FromDatabase, désérialisation

**Inbox** :
La table d'un module consommateur où le socle journalise les integration
events traités, dans la transaction du handler consommateur — c'est elle qui
rend la livraison at-least-once idempotente — et son store, jumeau de celui
de l'outbox : la paire (event, handler) y est la clé. Le marquage poison,
lui, vit sur la ligne d'outbox après épuisement des retries.
_Avoid_ : dédup maison, journal de consommation

**IntegrationEventStores** :
L'accès aux stores d'un module monté, et les deux politiques de scope
nommées : dans le scope de l'appelant — la ligne part avec la transaction
de la commande ou pas du tout, c'est la porte du publisher — ou dans un
scope à soi, donc hors de toute transaction de handler, la porte du
dispatcher et du suiveur de Signal. C'est la seule chose qu'un appelant a à
choisir, et c'est une décision transactionnelle. L'inbox du chemin de
livraison n'y passe pas : le dispatcher possède déjà le scope où elle
s'écrit.
_Avoid_ : factory de store, accès outbox, helper de scope

**Réservation** :
L'appropriation, par une instance de l'hôte, d'un lot de lignes d'outbox en
attente pour une durée bornée — le bail. Deux instances qui dépilent la même
outbox se partagent les lignes au lieu de les traiter deux fois ; une
instance qui disparaît rend les siennes à l'expiration du bail.
_Avoid_ : lock, lease, claim, verrou de lot

**Rétention** :
La durée pendant laquelle une ligne d'outbox livrée ou une ligne d'inbox
traitée reste en base avant d'être purgée. Les lignes en attente et les
lignes poison ne sont jamais purgées.
_Avoid_ : nettoyage, TTL, expiration

**Vivacité** :
Le process de l'hôte répond — rien de plus. C'est ce qu'un orchestrateur
demande pour décider de le tuer.
_Avoid_ : health, liveness, ping

**Disponibilité** :
Chaque base de module montée est joignable. C'est ce qu'un orchestrateur
demande pour décider d'envoyer du trafic. Une outbox en attente ou poison
n'est pas une indisponibilité.
_Avoid_ : readiness, healthy, up

**Corrélation** :
L'identifiant de trace W3C qu'une requête porte dans tous ses logs et dans sa
réponse d'erreur, propagé depuis l'appelant quand il le fournit.
_Avoid_ : correlation id, request id, trace identifier

**Acteur** :
Qui agit — humain ou système — identifié par l'identifiant opaque que le
fournisseur d'identité lui donne, sans autre attribut. Toute requête en a un :
Anonyme tant que personne n'authentifie. Le socle le nomme et le fournit ;
il n'authentifie ni n'autorise — c'est au cloneur de brancher son
fournisseur.
_Avoid_ : user, utilisateur courant, principal, caller, identity, auteur

**Instant** :
Le moment où un fait est établi — l'ouverture d'un compte, la
comptabilisation d'une écriture. Le Domain le reçoit, comme il reçoit
l'Acteur : une transition qui date un fait prend l'Instant en paramètre, elle
ne demande jamais l'heure ; c'est l'Application qui la demande au socle, et
une réaction à un fait reprend l'Instant du fait, jamais un second. Toujours
en temps universel.
_Avoid_ : timestamp, horodatage, now, date de création, heure système

**Description (OpenAPI)** :
La surface Application d'un module, décrite pour le front : les statuts que
ModuleController garantit, la forme unique d'erreur et ses codes. Dérivée du
code, jamais déclarée à la main ; un seul document pour tout le monolithe,
commité dans le repo. Distincte des Contrats (de module), qui s'adressent aux
autres modules.
_Avoid_ : contrat front, contrat HTTP, API publique, swagger

**Client (TypeScript)** :
L'artefact généré depuis la Description pour le front : types des commandes
et des Results, union des codes d'erreur. Jamais écrit à la main, jamais
édité — il se régénère.
_Avoid_ : SDK, client API, types partagés

**Télémétrie** :
Les traces et les métriques que l'hôte laisse sortir vers un collecteur —
requêtes HTTP, requêtes SQL, livraison d'outbox, jauges du socle — sur un
canal standard, sans propriétaire. Opt-in : un hôte sans collecteur
configuré n'exporte rien et n'en dit rien. Les logs n'en font pas partie,
ils restent sur la sortie standard ; la Corrélation fait le pont.
_Avoid_ : monitoring, observabilité, APM, tracing, OpenTelemetry (le canal,
pas le concept)

**Liste** :
Une lecture qui rend plusieurs éléments, sous la forme unique du socle :
paginée par page numérotée, recherchée sur les colonnes que son reader
déclare, filtrée par les propriétés typées et multi-valeurs de sa query,
triée par son reader — jamais par un paramètre. Sa query dérive de
`ListQuery`, sa réponse est une Page, son reader n'écrit que sa
déclaration au moteur du socle.
_Avoid_ : liste paginée, collection, résultats de recherche, grid

**Page** :
L'enveloppe que toute Liste rend — ses éléments, la page et sa taille, le
total, ses Facettes — toujours complète : au-delà de la dernière, une Page
vide avec son total, jamais une absence. Une forme du socle, pas un
Result : l'élément, lui, appartient à sa query.
_Avoid_ : PagedResult, réponse paginée, enveloppe

**Facette** :
Les valeurs présentes d'un filtre d'une Liste et leur compte, comptées hors
du filtre lui-même — le compte dit ce que cocher cette valeur donnerait.
Nommée comme le filtre qu'elle alimente, en camelCase, valeurs en chaînes
que le front traduit.
_Avoid_ : agrégation, bucket, compteur de filtre, refinement

**Signal** :
Le message nu que le socle pousse aux clients connectés quand un integration
event a été livré — la ligne d'outbox marquée livrée est la notification.
Il dit quoi (le discriminant) et où (la ressource : un genre stable en
kebab-case et un identifiant), jamais comment : le client refait son GET.
Opt-in par l'event, qui nomme sa ressource ; servi en SSE sur un flux global
filtrable, après livraison, sans rejeu.
_Avoid_ : notification, message, push, event client, websocket

**Porte (de qualité)** :
Une vérification que la CI tient et qu'une tâche mise rejoue à l'identique
sur le poste — même commande, un step de CI par tâche : build sans warning
(compilateur, API bannies, analyseurs, audit NuGet), format, Description
à jour, suite complète, front typé, linté, formaté, testé, construit, audité,
image déployée. Elle ne redit
pas ce que le build ou un test d'architecture tient déjà ; ce qu'aucune
porte ne tient est dit tel quel (le Style Rider, la couverture).
_Avoid_ : check, lint, quality gate, hook

**Gel** :
La liste des desserrages admis, tenue par un test du socle : les exemptions
d'analyseurs de `.editorconfig` avec leur section et leur sévérité, les
exclusions de la Couverture, les propriétés qui décident de ce que le build
refuse, les symboles bannis, et la liste des Portes elle-même. Ce qui n'y
est pas est interdit — un test ignoré, un avertissement supprimé sur place.
Desserrer une Porte reste légitime ; le geste est explicite, jamais un diff
d'une ligne.
_Avoid_ : baseline, allowlist, liste d'exceptions, freeze

**Couverture** :
La carte de ce que la suite exécute, par assembly et par ligne, hors code
généré, migrations, Probe et tests — mesurée par le data collector,
rendue par ReportGenerator, publiée dans la page du run de CI. Une mesure
qui dit où regarder, jamais une Porte : aucun seuil, hors de
`mise run check`.
_Avoid_ : seuil de couverture, coverage gate, pourcentage à tenir
