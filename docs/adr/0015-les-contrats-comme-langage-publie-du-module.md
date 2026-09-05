# Les Contrats comme langage publié du module

Ce qu'un module offre aux autres vit dans un projet dédié,
`LoreBank.<Module>.Contracts` : ses integration events et ses ports de
lecture publics — rien d'autre. C'est la **seule** surface qu'un autre module
a le droit de référencer : le compilateur devient le garde-fou de la
frontière, là où une règle « ne référencez pas l'Application du voisin »
reposerait sur une discipline que rien ne vérifie. La doctrine « un module =
6 projets » devient « 6, +1 `Contracts` si le module publie » — un module qui
ne publie rien n'a pas ce projet. Sa seule dépendance est
`SharedKernel.Contracts` (marqueurs et ports du socle, projet volontairement
sans aucune référence) : pas de `SharedKernel.Domain`, donc pas de VO — un
integration event ne peut porter que des primitives, **matériellement**.

Les types publiés sont **distincts** des types internes. Un integration event
est le jumeau plat d'un domain event (`MoneyDepositedIntegrationEvent` /
`MoneyDepositedDomainEvent`), mappé par un domain event handler du module —
même argument que les paramètres d'exceptions : la forme interne des VO n'est
pas un contrat public, et refactorer le domaine ne doit jamais casser un
consommateur. La publication est opt-in par construction : seuls les faits
qu'un handler mappe sortent. L'event porte ce que ses consommateurs
consomment, rien de plus (pas de solde dans un mouvement). Son identité sur
le fil est un **discriminant stable choisi** — `[IntegrationEvent(
"bank.money-deposited")]`, premier segment = module publieur, la grille des
codes d'erreur — jamais un nom de type .NET : renommer un namespace n'est pas
une migration de données. Le port de lecture publié (`IBankAccountsContract`)
suit la discipline des readers : DTOs plats, implémentation SQL chez le
module propriétaire (`ModuleReader`), `null` pour l'absence, test de relecture
de chaque colonne — in-process et lecture pure, pas d'escalade de transaction.
La projection locale chez le consommateur reste l'évolution documentée pour le
jour où le couplage temporel de ce port devient un problème.

## Options écartées

- **Publier les domain events tels quels** : zéro duplication, mais chaque
  refactoring interne devient un breaking change pour les consommateurs — la
  frontière n'existe plus. La duplication des jumeaux est le prix de la
  frontière, et elle est faible.
- **Les types partagés dans le SharedKernel** : tout le monde couplé à tout
  le monde, et le SharedKernel devient le fourre-tout qu'il ne doit pas être.
- **Référencer l'Application du module publieur** : abolit précisément la
  frontière que le montage par modules construit.
- **Un dossier `Contracts/` dans un projet existant** : la frontière ne
  serait qu'une convention de rangement — seul un projet séparé rend
  l'interdit compilable.
- **Le full name .NET comme discriminant** : gratuit aujourd'hui, une
  migration de données au premier renommage de namespace.
