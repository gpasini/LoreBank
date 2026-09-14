# LoreBank

Monolithe modulaire .NET en Clean Architecture / DDD. Ce repo est une base de
départ : clone-le, renomme, et construis tes modules métier sur ce socle.
Le module `Bank` sert d'exemple de référence pour construire un module ;
`Ledger` (la comptabilité, nourrie par les events de `Bank`) pour la
communication inter-modules.

> **Ce qui a le droit d'entrer ici** (ADR 0034) : ce qui s'applique à toute
> tâche, ce qu'aucun garde-fou ne tient, et le routage vers la skill ou l'ADR
> du geste en cours. Une règle tenue par un garde-fou **n'entre pas** — le
> test la rattrapera —, sauf si la découvrir tard oblige à *défaire* plutôt
> qu'à *corriger*. Tout le reste vit dans son ADR, sa skill ou son `docs/`.
> Un plafond de lignes est gelé par `QualityGateFreezeTest` : ce fichier ne
> grossit pas sans qu'on le décide.

## À chaque tâche

- **Le SDK .NET vient de mise** : `mise exec -- dotnet <cmd>`, jamais le
  dotnet du PATH.
- **Avant d'écrire, invoquer la skill du geste.** Elle porte la recette, ses
  garde-fous, et l'ordre de l'ADR 0032 — squelette, test, **RED observé**,
  corps. Le tableau du routage, plus bas, dit laquelle.
- **Avant de considérer un changement terminé** : `mise run check` à la
  racine — toutes les portes, dans l'ordre de la CI (`docs/qualite.md`) — et
  comportement démontré à l'exécution : des tests, ou un programme de
  vérification. Un fichier hors format se corrige par `mise run format`,
  jamais à la main.
- **Le périmètre est celui de la demande.** Une amélioration repérée en route
  devient une issue `needs-triage`, pas un élargissement silencieux du diff.

## Ce qu'aucun garde-fou ne tient

Ce bloc est le seul du fichier dont l'oubli ne rougit nulle part. Il est
court par construction : tout ce qui peut être tenu par un test l'est, et
sort d'ici. `docs/qualite.md` en tient le registre complet, portes comprises.

### Le test avant le code (ADR 0032)

Squelette minimal qui compile → test → **RED observé** → corps. Un RED, c'est
un test qui compile et échoue sur son assertion ; jamais une erreur de
compilation. Un test écrit après coup est taillé sur le code qu'il vient
d'écrire : il confirme au lieu de spécifier, et **aucune porte ne voit la
différence** — le test est vert, la couverture monte. Un test qui *spécifie*
s'écrit avant ; un test qui *épingle* un contrat (clés JSON, Description)
s'écrit après.

### Le CQS tient jusqu'au bord HTTP

**Une action qui mute ne renvoie aucune représentation** ; une action qui lit
en renvoie une. Le client qui veut l'état d'après fait un `GET`. C'est un
aller-retour assumé : une commande qui renvoie la ressource est aussi une
lecture, et sa représentation peut diverger de celle du `GET` sans que rien
ne le signale.

`CqsContractTest` le rattrape — mais après le record, le handler, l'action et
leurs tests. C'est la seule règle tenue par un test qui reste écrite ici :
la découvrir tard oblige à *défaire* la forme du use case, pas à corriger une
ligne.

### Le style des signatures et des appels

- Dès qu'une signature — constructeur (primaire inclus), méthode, opérateur,
  record positionnel — a plus d'un paramètre : retour à la ligne après la
  parenthèse ouvrante, un paramètre par ligne, parenthèse fermante sur sa
  propre ligne. Un seul paramètre reste sur la ligne.
- Même règle pour les invocations (appels, `new`, `throw new`) : dès deux
  arguments, un argument par ligne et **arguments nommés** (`amount: 0m`).
  Un appel à un seul argument reste inline et non nommé.
- Encodé pour Rider dans `.editorconfig` (`resharper_*`) — et tenu par Rider
  et la relecture seulement : Roslyn n'a pas d'équivalent, `dotnet format` ne
  le vérifie pas (ADR 0028). Le reste de `.editorconfig` (accolades, `var`,
  ordre des modificateurs, namespace = dossier, encodage, newline finale) est
  tenu par la porte Format.

## Le routage

### Le geste que tu t'apprêtes à faire

| Ce que tu fais | Skill | Décisions |
|---|---|---|
| Une donnée typée, ou un primitif qui circule nu | `nouveau-value-object` | ADR 0016 |
| Un concept à identité, une transition d'état | `nouvel-agregat` | ADR 0003, 0020, 0023, 0024 |
| Une réaction à un fait métier | `nouveau-domain-event-handler` | ADR 0003 |
| Une écriture exposée en HTTP | `nouvelle-commande` | ADR 0011, 0012, 0019, 0030 |
| Une lecture exposée en HTTP | `nouvelle-query` | ADR 0012, 0018, 0027 |
| Le modèle persisté change | `nouvelle-migration-schema` | ADR 0006 |
| Des lignes existantes à transformer | `nouvelle-data-migration` | ADR 0013 |
| Un bounded context qui n'existe pas | `nouveau-module` | ADR 0001, 0005, 0007, 0008, 0009 |
| Un écran du front — lecture, commandes, Signal, Liste | `nouvel-ecran` | ADR 0036 |
| Dérouler une issue de bout en bout | `ajouter-fonctionnalite` | `docs/agents/issue-tracker.md` |

Les skills vivent dans `.claude/skills/`, en Markdown : elles se lisent sans
Claude Code, et chacune porte la table de ses **Garde-fous** — la règle, et
le test qui rougit si elle casse.

### Le sujet sur lequel tu tombes

| Sujet | Où |
|---|---|
| Le vocabulaire du socle | `CONTEXT.md` |
| Les erreurs, du domaine au JSON | `docs/erreurs.md` · ADR 0012 |
| La Description OpenAPI et le Client | `docs/openapi.md` · ADR 0019 |
| Les portes de qualité, le Gel, la couverture | `docs/qualite.md` · ADR 0028, 0029, 0031 |
| Le Signal (SSE) | `docs/signaux.md` · ADR 0026 |
| Le front : la frontière socle / exemple, ses tests, son Gel | `docs/front.md` · ADR 0036 |
| La Télémétrie | `docs/telemetrie.md` · ADR 0025 |
| Le montage d'un module dans l'hôte | ADR 0001, 0005, 0007, 0008, 0009 |
| Les integration events, outbox et inbox | ADR 0014, 0015, 0021 |
| Les readers, les rows keyless | ADR 0018 |
| Les repositories d'agrégat | ADR 0010 |
| L'observabilité : logs, Corrélation, santé | ADR 0022 |
| La Liste, la Page, les facettes | ADR 0027 |
| La Version d'agrégat et la concurrence | ADR 0020 |
| L'Acteur et l'Instant | ADR 0023, 0024 |
| Les migrations, hors du processus API | ADR 0006, 0013 |
| Le harnais d'intégration, le Probe, le DbSetup | ADR 0002, 0017, 0030 |
| La validation aux frontières, la réhydratation | ADR 0016 |
| Le dispatch des domain events | ADR 0003 |
| Les hooks versionnés de Claude Code | ADR 0033 |
| Les règles sans garde-fou : tenues ou assumées | `docs/qualite.md` · ADR 0035 |
| Ce fichier-ci | ADR 0034 |
| Toutes les décisions, par thème et statut | `docs/adr/README.md` · ADR 0037 |

`docs/agents/` porte les conventions de travail : issue tracker, labels de
triage, domain docs. `AGENTS.md` renvoie ici pour les agents qui suivent
cette convention plutôt que celle de Claude Code.
