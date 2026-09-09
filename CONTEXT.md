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
(jamais un nom de type .NET). Un dispatcher de l'hôte la dépile hors
transaction.
_Avoid_ : file d'attente, bus

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
La surface d'observation d'outbox du harnais : lit les lignes (discriminant,
payload, livré) et vide l'outbox d'un module désigné par son DbContext — le
garde-fou de publication d'un module se réduit à agir puis affirmer. N'expose
ni retries, ni poison, ni inbox : des invariants du socle, pas d'un module.
_Avoid_ : helper SQL d'outbox, lecteur d'outbox

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
rend la livraison at-least-once idempotente et porte le marquage poison après
épuisement des retries.
_Avoid_ : dédup maison, journal de consommation

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
