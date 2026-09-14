# La relecture : un lecteur frais, pas l'auteur

> Statut : accepté — 2026-09-14. Complète l'ADR 0035, qui renvoyait à #32
> pour « le reste de la doctrine » — les règles qu'aucun test ne sait
> formuler.

`docs/qualite.md` dit trois fois « la relecture le tient » : le style des
signatures, un `Result` maison nommé hors de l'heuristique de
`DomainConventionTest`, un type d'API réécrit à la main côté front. L'ADR
0035 a fait le tri : ce qui pouvait être tenu par un test l'est, le reste
est **assumé** et écrit dans cette liste. Mais « la relecture » n'était
définie nulle part. En pratique, c'était la session qui venait d'écrire le
code — celle qui a le plus de raisons de trouver son diff satisfaisant, et
dont le contexte est saturé de ses propres justifications.

Une part de la doctrine ne sera jamais testable : le vocabulaire du
glossaire plutôt que ses synonymes, un `Result` qui redouble celui d'à côté,
une exception trop générique, un commentaire qui paraphrase le code,
l'altitude d'une abstraction, un ADR contredit sans être rouvert. Ce sont
des jugements, et un jugement ne se rejoue pas. Ce qui peut changer, c'est
**qui** juge.

## La décision

**Un sous-agent versionné, `.claude/agents/relecteur.md`**, relit le diff à
contexte frais : il reçoit la base du diff et le numéro de l'issue, lit les
décisions grillées dans ses commentaires, puis le diff, puis une checklist
qu'il **dérive** des docs — la section « Ce qu'aucune porte ne tient » de
`docs/qualite.md`, le bloc Style de `CLAUDE.md`, les lignes _Avoid_ de
`CONTEXT.md`, les ADR des thèmes que le diff touche via l'index — et le
voisinage de chaque fichier touché. Il rend des **écarts**, ou « Aucun
écart » : `fichier:ligne`, la règle et sa source, un verdict — *à
corriger*, *à assumer* (donc à écrire dans `docs/qualite.md`), ou *contredit
l'ADR NNNN* (à rouvrir). Rien d'autre.

**Il ne tient que ce qu'aucun test ne tient.** Il ne relance aucune porte,
ne redouble aucun test, ne juge pas la conformité à la spec (la revue
humaine le fait) et ne commente pas le RED (une trace qu'il ne peut pas
vérifier). Lecture seule.

**L'issue est sa mémoire.** L'objection au sous-agent — il ne voit pas la
conversation, donc redemande ce qui a été justifié — tombe parce que ce qui
a été décidé en déroulant un chantier s'écrit dans les commentaires de son
issue (`ajouter-fonctionnalite`). Ce qui a été justifié hors de l'issue n'a
pas été justifié : c'est la même règle que pour le RED.

**Il se déclenche par consigne**, parce qu'il ne peut pas se déclencher
autrement : un hook lance une tâche mise du repo, jamais un agent, et
n'ajoute aucune règle (ADR 0033). La puce « Avant de considérer un
changement terminé » de `CLAUDE.md` le nomme, et `ajouter-fonctionnalite`
en fait son étape 6, entre la vérification et la clôture. **Chaque écart
se traite** — corrigé dans le diff, assumé dans `docs/qualite.md`, rouvert
en commentaire, ou rejeté en disant pourquoi — et le récap de l'issue porte
une section « Relecture » qui le dit. Le rejet silencieux est le seul vrai
échec.

**La checklist n'est pas recopiée.** L'agent pointe les docs, il ne les
cite pas : une copie divergerait à la prochaine règle assumée, et
`docs/qualite.md` resterait la seule source pour tout le monde sauf lui.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| La relecture a eu lieu, ses écarts ont été traités | **rien** — la section « Relecture » du récap en est la trace, comme « RED observés » pour le RED, et ne couvre que le travail passé par le chapeau |
| L'agent cite `ADR 0038`, `docs/qualite.md` renvoie à l'agent | `AdrIndexTest` pour les citations d'ADR ; rien pour le reste |

**Ce n'est pas un garde-fou au sens du Gel**, et il ne passe pas sous lui :
un jugement ne se rejoue pas, l'agent ne tourne que chez Claude Code, et
il reste inerte pour un cloneur qui travaille autrement — le même statut
que les hooks (ADR 0033). Le geler obligerait ce cloneur à conserver un
outillage qu'il ne peut pas exécuter. Ce qu'il tient est donc **assumé**,
écrit dans la liste de `docs/qualite.md`, avec sa trace.

## Options écartées

- **Une skill de relecture** invoquée par la session en fin de chantier.
  Elle voit toute la conversation — c'est exactement le problème : elle
  relit avec les justifications de l'auteur dans son contexte. Le contexte
  frais est le mécanisme, pas un détail. Et l'information qui manque au
  sous-agent est dans l'issue, où elle doit être de toute façon.
- **Un hook de fin de tour** qui lancerait la relecture. Impossible par
  construction (un hook lance une commande, pas un agent) et interdit par
  l'ADR 0033 : un hook n'appelle que ce que le repo contient et n'ajoute
  aucune règle.
- **Une checklist recopiée dans l'agent**, pour qu'il se suffise. Deux
  listes qui disent la même chose divergent ; `docs/qualite.md` est déjà la
  liste, tenue par l'ADR 0035, et l'index des ADR (0037) rend les ADR
  trouvables par thème. L'agent lit, il ne porte pas.
- **Relire aussi la conformité à la spec.** C'est le geste de la revue
  humaine qui ferme l'issue ; le doubler diluerait le signal de doctrine
  dans du déjà-relu, et donnerait au relecteur une raison de résumer.
- **Un template de PR dans `.github/`** portant la checklist. Ce repo ne
  fait pas de PR : ce serait de la doctrine que personne n'exerce, et une
  seconde copie de `docs/qualite.md`. Un cloneur en PR lie la section « La
  relecture » de `docs/qualite.md`.
- **Le mettre sous le Gel** — un test qui vérifie que l'agent existe et
  que `CLAUDE.md` le nomme. Voir Garde-fous : ce serait traiter un jugement
  comme une Porte.
