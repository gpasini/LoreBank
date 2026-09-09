# L'authentification hors du socle, l'Acteur dedans

> Statut : accepté — 2026-09-09.

Le template n'authentifiait ni n'autorisait rien, et aucun texte ne disait
si c'était un choix. On décide que ça l'est : le socle **n'authentifie ni
n'autorise** — pas de pipeline monté, pas de garde sur `ModuleController`,
pas de 401/403 dans la forme d'erreur, pas de security scheme dans la
Description — parce qu'un schéma d'authentification est un choix de
fournisseur (Keycloak, Entra, Auth0, un jeton émis ailleurs) que chaque
cloneur hériterait sans l'avoir fait, comme un logger ou un exporteur
(ADR 0022). L'hôte cible valide un jeton, il n'émet rien et ne stocke aucun
compte. Mais le socle **nomme qui agit** et fixe le seul point où l'identité
entre : l'**Acteur**, un value object du SharedKernel.Domain qui porte
l'identifiant opaque donné par le fournisseur (le `sub`, un `string` — jamais
un Guid, hypothèse qu'aucun fournisseur ne tient) et rien d'autre, avec un
état Anonyme ; un port de l'Application (`ICurrentActor`) le fournit aux
handlers de commande, qui le passent en paramètre aux transitions d'agrégat
qui veulent enregistrer leur auteur — le Domain ne demande jamais « qui
agit », il le reçoit. L'implémentation du socle lit le principal
qu'ASP.NET Core expose de toute façon : Anonyme sans schéma, l'identifiant
(`NameIdentifier`, puis `sub`) dès que le cloneur monte le sien — zéro ligne
à changer dans les modules ce jour-là. Un principal authentifié sans
identifiant est une erreur de configuration, pas un Anonyme : le port lève.
Anonyme se représente par `null` hors du Domain (colonne nullable, `null`
dans un Result), la forme de l'absence que les readers ont déjà — une valeur
réservée pourrait être contrefaite par un `sub` réel. Le module de référence
montre le chemin sur une seule transition : l'ouverture d'un compte
enregistre son Acteur, sans refuser un Anonyme — le template tourne sans
rien brancher, un compte ouvert par Anonyme est un fait honnête.

## Options écartées

- **Un seam minimal sans package** (pipeline monté avec un schéma vide,
  garde sur `ModuleController`, schéma de test dans le harnais, 401/403
  typés) : plus de code du socle pour un fournisseur qui n'est pas là, et
  le schéma de test prouverait le framework, pas le socle. Ce que le cloneur
  y gagnerait tient dans la procédure ci-dessous.
- **Un bearer JWT d'exemple avec un émetteur de développement** : un package
  et un fournisseur choisis à la place du cloneur.
- **L'identité de l'hôte** (comptes en base, mots de passe, ASP.NET
  Identity) : une persistance entière dans le socle ; un module
  « Utilisateurs » serait un module métier comme un autre.
- **Le port dans le Domain** (`Services/`) : aucun objet du Domain ne le
  consomme — un agrégat reçoit son Acteur en paramètre.
- **Des rôles ou un nom d'affichage dans l'Acteur** : les rôles sont de
  l'autorisation, hors périmètre ; le nom est une donnée de lecture, pas une
  identité. Un module qui en a besoin les ajoute dans son propre VO.

## Conséquences

- **Brancher son fournisseur** : `AddAuthentication().AddXxx(...)` et
  `UseAuthentication()` dans l'hôte, avant `MapControllers` ; pour fermer
  par défaut, une `FallbackPolicy` qui exige un utilisateur authentifié et
  `UseAuthorization()` — aucun `[Authorize]` à recopier par module. Les
  401/403 qui apparaissent alors doivent rejoindre `ApiProblem` (un `code`
  sans préfixe, pas de `detail`) et la liste des statuts de la Description ;
  le harnais gagne un schéma de test pour agir « en tant que » sur HTTP.
- **Agir « en tant que » dans les tests** se fait par un fake du port dans
  la factory du module, posé par le test avant l'arrange et remis sur
  Anonyme par `ResetFakes`.
- Les surfaces d'administration de l'outbox (liste des lignes poison,
  réactivation — ADR 0022) attendent ce branchement : elles seraient
  publiques.
