# Le hook : un moment, pas une règle

> Statut : accepté — 2026-09-11. Corrige l'ADR 0032, qui renvoyait à ce
> chantier un garde-fou que la réserve ci-dessous exclut.

Les Portes de ce repo sont outillées (ADR 0028) et leurs desserrages sont
gelés (ADR 0031). Il restait un trou d'un genre différent : **rien ne
vérifiait qu'elles avaient tourné**. « `mise run check` est passé » était une
affirmation de l'agent, jamais une observation — et c'est le mode de
défaillance le plus fréquent et le moins visible, le seul que la relecture
humaine ne rattrape pas, puisque le diff a l'air fini.

Trois familles de fichiers sont par ailleurs générées, et une seule n'est
rattrapée par rien : les seize `*.Designer.cs` et `*ModelSnapshot.cs` d'EF.
Une retouche à la main y passerait la CI et casserait la base au
déploiement. Et les neuf skills ne se déclenchent que si l'agent les invoque :
rien ne les rappelle au moment où il écrit dans `*.Domain/ValueObjects/`.

## La décision

Des **hooks versionnés** dans `.claude/settings.json`, donc hérités par le
cloneur — sous une réserve qui les définit entièrement.

### La réserve

Un hook est un **sixième type de garde-fou**, et le seul qui ne se rejoue
pas : il ne s'exécute que chez Claude Code, n'apparaît pas dans la CI, et
reste inerte pour un cloneur qui travaille autrement. Il est admis **à
condition de n'ajouter aucune règle**. Il lance une Porte existante, refuse
un fichier généré, ou rappelle une skill — rien d'autre. Toute règle reste
tenue par une Porte rejouable hors de Claude Code.

Corollaire, et c'est la contrainte qui protège le template : **un hook
n'appelle que ce que le repo contient.** Jamais une commande de plugin,
jamais un outil que `mise install` ne pose pas.

### Les trois hooks

| Événement | Tâche | Ce qu'il fait |
|---|---|---|
| `Stop` | `hook:verify` | Lance les Portes rapides, constate que `check` n'a pas tourné depuis la dernière édition, et **rappelle une fois** |
| `PreToolUse` (`Edit\|Write`) | `hook:generated` | Refuse l'édition à la main d'un fichier généré, avec le geste correct |
| `SessionStart` | `hook:skills` | Pose la table chemin → skill dans le contexte |

### La logique vit dans des tâches mise

Pas de scripts dans `.claude/`, pas de commandes inline. Trois raisons qui se
renforcent : mise est déjà le prérequis du repo (`mise install` est la
première ligne du README) ; la contrainte ci-dessus devient littéralement
vraie ; et surtout **la logique reste rejouable hors de Claude Code** — un
cloneur qui n'en veut pas tape `mise run hook:verify` dans son terminal et
obtient la même réponse. Un `.sh` caché dans `.claude/` n'aurait eu aucune
des trois propriétés, et serait resté inerte sous Windows hors WSL, quand un
template doit cloner partout.

`jq` est épinglé dans le `[tools]` du `mise.toml` racine : les hooks
reçoivent leur entrée en JSON sur stdin, et un parseur JSON en `sed` n'en
serait pas un.

### Le rappel se fait une seule fois

`hook:verify` lit `stop_hook_active` et sort immédiatement s'il est levé.
Un rappel par tour, jamais deux — le plafond dur de huit blocages
consécutifs ne sert jamais. Et son message dit explicitement qu'un arrêt
délibéré — l'agent s'arrête pour poser une question, faire trancher un
choix — est légitime : il suffit de le redire. **Un blocage trop zélé est
pire que pas de hook du tout.**

Ce qu'il surveille : les **fichiers suivis**, et eux seuls. Le scan
couvrait d'abord aussi les non suivis non ignorés, pour attraper un `.cs`
créé et jamais `git add` — le cas d'un agent en plein travail. L'usage a
tranché autrement dès le premier tour réel : un brouillon posé à côté, un
fichier de sortie, et le rappel tombait sans rien apprendre. Le prix est
connu et assumé — un fichier neuf passe sous le radar jusqu'à son premier
`git add`, et c'est le commit qui le rattrape.

Le message **nomme le fichier** qui l'a déclenché. Sans lui, un rappel se
discute au lieu de se traiter : la première fois que ce hook a bloqué pour
de vrai, il a fallu dix minutes et trois hypothèses fausses pour retrouver
quelle édition l'avait réveillé. Une ligne de plus dans le motif remplace
l'enquête.

L'horodatage est la neuvième entrée de `[tasks.check]`, écrite en dernier
donc seulement si tout est vert. Ce n'est pas une Porte : le Gel sépare les
huit au préfixe `mise run //`.

### Ce que le cloneur voit, et ce qu'il perd

Les hooks d'un projet **ne s'exécutent pas en silence** : ils exigent la
confiance de l'espace de travail. Au premier ouvrage du repo cloné, une
invite demande si l'on fait confiance ; tant que non, aucun hook du projet ne
tourne, et `/hooks` les rend inspectables avant. C'est ce qui rend un hook
versionné acceptable dans un template.

Un cloneur qui travaille sans Claude Code ne perd aucune règle — par
construction, puisqu'ils n'en portent aucune. Il perd le *moment* : plus
personne ne lui rappelle de lancer les Portes. Les trois tâches `hook:*`
restent à sa disposition dans son terminal. Le README le dit.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| La neuvième entrée de `[tasks.check]` (l'horodatage) retirée ou changée | `QualityGateFreezeTest` (le Gel, ADR 0031) |
| Un hook supprimé, ajouté ou détourné | **rien** — et c'est délibéré, voir ci-dessous |

**Les hooks ne passent pas sous le Gel.** Les figer les traiterait comme des
Portes, ce que cet ADR nie : ils n'ajoutent aucune règle, ne tournent que
chez Claude Code, et sont inertes pour un cloneur qui travaille autrement.
Les geler l'obligerait à conserver un outillage qu'il ne peut pas exécuter,
sous peine de suite rouge. Le Gel garde les garde-fous **rejouables** ; un
hook ne l'est pas — c'est précisément ce qui en fait un sixième type, et ce
qui lui interdit de porter une règle. Les tâches `hook:*`, elles, sont
rejouables, mais elles ne sont pas dans `[tasks.check]` : le Gel ne les voit
pas, et c'est cohérent.

## Options écartées

- **Un hook qui vérifie le RED de l'ADR 0032** — qu'un test rouge a précédé
  le vert. C'est la seule chose qui fermerait la limite de la preuve par
  récap, et l'ADR 0032 la renvoyait ici. **La réserve l'exclut** : vérifier
  un RED n'est ni lancer une Porte existante, ni refuser un fichier généré,
  ni rappeler une skill — aucune Porte ne tient le RED, donc un hook qui
  l'imposerait ajouterait une règle que rien ne rejoue. Le RED reste sans
  garde-fou mécanique, et `docs/qualite.md` le recense comme tel.
- **Le routeur de skills en `PreToolUse`**, comme le chantier l'imaginait :
  matériellement impossible. `PreToolUse` ne sait qu'autoriser, demander ou
  refuser — il ne peut pas injecter de contexte. « Rappeler » la skill
  n'aurait pu se faire qu'en **refusant** l'édition, un blocage dur sur une
  action légitime. `SessionStart` pose la table pour le prix d'un `cat`.
- **Des scripts `.sh` dans `.claude/hooks/`** : non rejouables comme une
  tâche du repo, inertes sous Windows hors WSL, et une deuxième famille
  d'exécutables dans un repo qui en a déjà une.
- **Un parseur JSON en `sed`/`grep`** pour éviter la dépendance `jq`, dans un
  repo qui bannit l'échappatoire SQL dans ses readers. `python3` n'est ni
  garanti ni épinglé.
- **Bloquer tant que les Portes n'ont pas tourné**, sans le garde
  `stop_hook_active` : l'agent ne pourrait plus poser une question sans
  franchir huit blocages. Le rappel unique préserve l'arrêt délibéré.
- **Geler les hooks** : voir Garde-fous.
- **Trancher ici le sort d'`enabledPlugins`**, que ce fichier versionne
  aussi : l'issue #13 porte les quatre références du plugin dans
  `docs/agents/` et la question de l'exemple d'issue embarqué. Trancher
  depuis le seul fichier de config aurait décidé pour elle.

## Le coût assumé

`hook:verify` lance `mise run pre-commit` avant de rappeler, pour que sa
sortie dise *quoi* corriger plutôt que d'envoyer voir ailleurs. Or
`dotnet format --verify-no-changes` compile la solution : une à deux minutes
à froid, une à deux secondes à chaud (`docs/qualite.md`). La session est
bloquée pendant ce temps, au premier arrêt d'un repo froid seulement. C'est
le prix d'un message actionnable ; retirer cet appel rendrait le hook
instantané et son message plus vague.
