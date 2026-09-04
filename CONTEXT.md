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

**HostModule** :
La base des adapters `IHostModule`. Dérive l'identité du module — ses trois
assemblies, son nom et sa persistance par défaut — du DbContext ancré en
paramètre générique ; un adapter ne déclare plus que son `Module` Autofac.
_Avoid_ : ModuleIdentity, adapter d'hôte
