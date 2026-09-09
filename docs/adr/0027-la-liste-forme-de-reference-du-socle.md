# La Liste : la forme de référence d'une lecture paginée, recherchée, facettée

> Statut : accepté — 2026-09-09.

`ListBankAccounts` rendait la table entière, sans borne, et rien dans le
socle ne fixait la forme d'une liste : chaque module aurait choisi la
sienne — offset ou curseur, enveloppe ou pas, noms de paramètres — et le
front en aurait appris plusieurs. Le grilling a élargi la question : une
liste utile est paginée, mais aussi recherchée et filtrée, et un filtre
utile s'accompagne de sa facette — les valeurs présentes et ce que cocher
chacune donnerait. C'est cet ensemble, la **Liste**, que le socle fixe une
fois, avec son moteur, pour qu'un reader n'écrive plus jamais un
`Skip/Take`, un `COUNT` ni le `GROUP BY` d'une facette.

## La forme

- **La query dérive de `ListQuery<TItem>`** (`SharedKernel.Application`) et
  se lie `[FromQuery]` sur la query string, comme le body se lie sur une
  commande (ADR 0012). La base porte `page` (≥ 1), `pageSize` (20 par
  défaut, 100 au plus — des constantes du socle, non surchargeables : deux
  listes aux bornes différentes seraient deux formes) et `search`. La query
  dérivée déclare ses **filtres** : des propriétés typées, toujours
  multi-valeurs (`IReadOnlyList<T>` — `currency=EUR&currency=USD` : OU dans
  un filtre, ET entre filtres, la sémantique que des cases à cocher
  impliquent). Le **tri** n'est pas un paramètre : il est le sens de la
  lecture, fixé par le reader — une liste triée autrement est une autre
  query.
- **La réponse est une Page**, `ListPage<TItem>` : `items`, `page`,
  `pageSize`, `totalCount`, `facets`. L'item reste le Result propre à la
  query (ADR 0012) ; l'enveloppe est une forme du socle, comme `ApiProblem`
  l'est pour les erreurs, pas un Result. Toujours complète : une page
  au-delà de la dernière est une Page vide avec son `totalCount`, jamais
  un 404.
- **La recherche** est une sous-chaîne insensible à la casse (`ILIKE`,
  jokers échappés) sur les colonnes que le reader déclare. Pas de
  full-text : ce sera une évolution par query, avec son index, le jour où
  une colonne de texte libre existe.
- **Une Facette** porte le nom camelCase du filtre qu'elle alimente —
  déclarés d'un même geste, par `nameof` — et ses valeurs présentes, en
  chaînes (le front traduit `"true"`), comptées **hors de son propre
  filtre** : cocher « EUR » ne fait pas disparaître « USD » de la facette
  devise, mais restreint la facette clôture à ce que EUR contient. Comptes
  décroissants puis valeur.
- **Hors bornes, 422 `INVALID_PAGING`** (`page`, `pageSize`, `maxPageSize`),
  levé par le moteur — donc la même règle par HTTP et par `ISender`. Un
  `page=abc` reste le 400 de binding, dont le champ fautif est nommé comme
  la Description le nomme : en camelCase.

## Le moteur

`Query<TRow>().List(query)` (`SharedKernel.Infrastructure/Readers`) ouvre la
déclaration : `SearchIn(colonne)`, `Filter(query.Xxx, colonne, facet:
nameof(query.Xxx))`, `OrderBy(colonne)`, puis `ToPageAsync(projection)`
exécute — le `COUNT`, un `GROUP BY` par facette sur la recherche et les
autres filtres, la page projetée où EF ne lit que les colonnes du Result.
Un reader qui liste n'écrit que cette déclaration ; le port de lecture
reçoit la query entière. LINQ seulement, sur la row keyless (ADR 0018) :
le moteur est fidèle au provider du socle (ADR 0008) et se prouve sur
PostgreSQL, pas sur Sqlite.

## La Description et le front

Les paramètres de query string sortent en camelCase comme les clés JSON
(`DescriptionDocumentTransformer`), les entiers en `integer` comme les
décimaux en `number`, et la réponse est le schéma que le générateur dérive
du générique (`ListPageOfBankAccountSummaryResult`). Le Client en tire des
paramètres typés (`currency?: string[]`, `isClosed?: boolean[]`) et la
forme de la Page. Le front porte les briques réutilisables d'une Liste
(`frontend/src/listing/` : état, recherche à debounce, facettes en cases à
cocher, pager) ; un écran ne les assemble que pour sa query. Changer la
recherche ou un filtre ramène en page 1, la relecture sur Signal rejoue
les paramètres courants, l'état vit dans React, pas dans l'URL.

## Garde-fous

`ListContractTest` (socle, sur la sonde de Liste du Probe : défauts,
tranches, page au-delà, recherche et jokers, OU/ET, facettes disjonctives,
422, 400 en camelCase), `DescriptionContractTest` (paramètres camelCase et
typés, `ListPageOf…`), `ApplicationConventionTest` (une query qui rend une
Page dérive de `ListQuery`, ses filtres sont multi-valeurs),
`ErrorCodesDescriptionTest` (`INVALID_PAGING` dans l'enum) ; côté Bank,
`ListBankAccountsTest` et `CqsContractTest` prouvent l'emprunt du chemin.

## Options écartées

- **Le curseur (keyset)** : stable sous insertion, mais ni total ni saut à
  une page, et une clé de tri unique à garantir par query. L'écran est un
  écran de gestion, pas un fil infini ; la page numérotée est la forme que
  tout cloneur reconnaît. Une évolution par query reste possible.
- **La convention par query** (chaque Result redéclare `page`, `pageSize`,
  `totalCount`) : invérifiable par un test d'architecture, et autant de
  formes que de modules pour le front.
- **Les filtres en dictionnaire** (`filter[currency]=EUR`) : réutilisable
  sans rien déclarer, mais le Client ne sait plus quels filtres existent et
  la Description ne peut pas les décrire. La query typée est ce qui garde
  ADR 0012 intact.
- **Le tri paramétrable** (`sort=`) : le tri est le sens d'une lecture ; une
  liste triée autrement est une autre query avec son Result.
- **Les facettes conjonctives** (comptées sur l'ensemble entièrement
  filtré) : les valeurs non cochées d'une facette cochée afficheraient 0,
  la forme e-commerce est celle que les utilisateurs connaissent.
- **Le 400 par DataAnnotation** (`[Range]` sur la base) : un appelant
  `ISender` passerait ; le moteur lève, une seule règle quel que soit le
  chemin.
- **Un `PageRequest` embarqué** (`query.Paging.Page`) : `paging.page=2` sur
  le fil. La base de query porte les paramètres à plat.
- **Un `IBindingMetadataProvider` pour le camelCase** : renommer les noms
  de binding de toutes les propriétés fait boucler la génération de la
  Description au build. Le transformer renomme les paramètres décrits, le
  binding reste insensible à la casse.
- **Paginer aussi les mouvements du Ledger** : une liste imbriquée dans un
  détail devient sa propre query — l'issue 18 du chantier `revue-template`.
