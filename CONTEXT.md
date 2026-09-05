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
La base des readers d'un module métier. Porte l'emprunt de connexion et le
schéma du module ; un reader concret ne fournit que son SQL, ses paramètres
et sa lecture de colonnes.
_Avoid_ : reader de base, helper SQL

**ModuleRepository** :
La base des repositories d'agrégats d'un module métier. Porte le chargement
requis (l'absence lève la NotFoundException du module), le mini-unit-of-work
et le passage par le SaveChangesAsync qui dispatche les events ; un repository
concret ne fournit que sa fabrique d'exception.
_Avoid_ : repository générique, repository de base

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

**Inbox** :
La table d'un module consommateur où le socle journalise les integration
events traités, dans la transaction du handler consommateur — c'est elle qui
rend la livraison at-least-once idempotente et porte le marquage poison après
épuisement des retries.
_Avoid_ : dédup maison, journal de consommation
