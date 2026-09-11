# CLAUDE.md : un routeur, pas un manuel

> Statut : accepté — 2026-09-11. Renverse le choix de la revue #5, qui avait
> écarté l'allègement (« pas d'issue, à traiter à la main »).

`CLAUDE.md` pesait **705 lignes, 48,2 Ko** — environ 12 000 tokens chargés
d'office à chaque session, avant la première question. Il en pesait 34 Ko le
2026-09-09, 47 Ko le 10, et **+1,2 Ko dans la seule journée du 11**, en trois
ajouts : le Gel, le RED, la clause des hooks. Il grossit à chaque chantier
parce que chaque chantier y ajoute son paragraphe. C'est un mécanisme, pas un
accident, et rien ne l'arrêtait.

Le coût n'est pas le prix des tokens. C'est la **dilution** — une règle au
milieu de 705 lignes de prose narrative concourt avec toutes les autres, et
les trente premières lignes sont lues bien mieux que les trois-centièmes — et
c'est la **divergence silencieuse** : une règle écrite dans `CLAUDE.md`, dans
son ADR et dans la table Garde-fous d'une skill a trois exemplaires et une
seule source de vérité. Rien ne rougit quand ils se contredisent.

Ce qui rend l'allègement sûr ici, et pas dans un repo quelconque : le fichier
citait déjà **28 ADR distincts sur 33** et **32 noms de tests**. Ce n'était
pas un document qui ignorait ses sources — c'était un document qui les
**redisait** au lieu d'y renvoyer.

## La décision

`CLAUDE.md` ne porte plus que trois choses, dans cet ordre :

1. **Ce qui s'applique à toute tâche.**
2. **Ce qu'aucun garde-fou ne tient** — le bloc dont l'oubli ne rougit nulle
   part.
3. **Le routage** — geste → skill → ADR, et sujet → doc.

Les règles qu'aucun test ne rattrape viennent en **deuxième**, pas en
dernier : les enterrer à la fin reproduirait la dilution qu'on combat. Le
tableau de routage se consulte, il n'a pas besoin d'être lu.

### Le critère de tri

> Une règle que **rien ne tient** reste. Une règle tenue, mais dont la
> violation découverte tard oblige à **défaire**, reste. Une règle tenue dont
> la violation oblige seulement à **corriger** sort : le test la rattrapera.

L'axe n'est pas *quand* le garde-fou rougit, c'est *ce que coûte la reprise*.
« Un garde-fou qui rougit tôt » classe mal : `ModuleCompositionTest` rougit
tard — il faut lancer les tests d'intégration — mais la reprise coûte une
ligne. À l'inverse, `CqsContractTest` rattrape « une commande ne renvoie
aucune représentation » **après** le record, le handler, l'action et leurs
tests : là, il faut défaire la forme du use case. C'est la seule règle tenue
par un test qui soit restée écrite dans `CLAUDE.md`.

### Ce qui sort ne va nulle part

Puisque 28 ADR sur 33 étaient déjà cités, l'essentiel de la prose redisait un
ADR ou une recette de skill **qui existe**. Pour ce contenu-là, la
destination était déjà écrite : retirer suffit, et c'est le but — supprimer
un des trois exemplaires supprime la divergence. Le critère se formule donc
« rien n'a disparu **sans que sa source reste** », et aucun document nouveau
n'a été nécessaire.

Résultat : **705 → 144 lignes, 48,2 → 7,6 Ko**, et le routage couvre 32 des
33 ADR (le 0004 est remplacé par le 0018) et les neuf skills — c'est-à-dire
**plus** de sources qu'avant, dans un sixième de la place.

### Le fil tendu en travers de la porte

Une règle en tête du fichier dit ce qui a le droit d'y entrer. Seule, ce
serait de la prose que rien ne tient — exactement le reproche fait au reste.
Elle est donc doublée d'un **plafond de lignes gelé** par
`QualityGateFreezeTest` (ADR 0031).

Le plafond porte sur les **lignes**, pas les octets : c'est le coût de
lecture, et c'est stable à la reformulation. Il a du **mou** — un rond à
~15 % au-dessus de l'atteint : assez pour qu'une phrase réécrite ne le
touche pas, assez peu pour qu'une section nouvelle le franchisse. Un
décompte exact, comme les autres listes gelées, rougirait pour rien et
s'apprendrait à contourner. Et son message porte **le critère**, pas le
nombre : on ne relève pas un plafond, on fait de la place — ou on déclare.

### `CONTEXT.md` est le modèle

Il n'est **pas** chargé d'office — rien ne l'injecte, ni le socle ni les
hooks — donc il ne dilue rien, et on n'y touche pas. Mieux : sa forme — une
entrée par concept, dense, avec ses `_Avoid_` — est celle vers laquelle
`CLAUDE.md` devait tendre.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| `CLAUDE.md` dépasse son plafond de lignes | `QualityGateFreezeTest` (le Gel, ADR 0031) |
| Le critère de tri lui-même | **rien** — c'est un jugement, pas une mesure. Le plafond ne dit pas *quoi* retirer, il force la conversation |

## Options écartées

- **Ne rien faire**, le choix de la revue #5 le 2026-09-09 (« à traiter à la
  main »). Quatre chantiers plus tard, la main n'était pas venue, et le
  fichier avait pris 14 Ko. Un geste qui dépend de la discipline dans un repo
  qui outille tout le reste n'avait aucune raison de tenir.
- **Garder les neuf sections par couche et les élaguer en place** : c'est
  leur structure qui invitait la prose narrative et offrait à chaque chantier
  un endroit où poser son paragraphe. Élaguer sans changer la forme aurait
  laissé le mécanisme intact.
- **Un `docs/architecture.md` unique** pour accueillir ce qui sort : déplacer
  le problème, et créer le gros document de plus qu'on voulait éviter.
- **Compter en octets** plutôt qu'en lignes : une reformulation les fait
  bouger sans rien changer au coût de lecture.
- **Un plafond exact**, comme les treize sévérités ou les cinq propriétés
  gelées : ici, il rougirait à chaque phrase réécrite. Une garde qui rougit
  pour rien s'apprend à contourner.
- **Alléger aussi `CONTEXT.md`** : il n'est pas chargé d'office, il ne dilue
  rien, et sa forme est déjà la bonne.
- **Déplacer le routage dans le hook `SessionStart`** plutôt que dans
  `CLAUDE.md` : le coût en tokens serait identique — déplacer n'économise
  rien, seul supprimer économise — et un agent hors Claude Code, ou avant la
  confiance accordée, n'y aurait pas accès, ce qui est précisément ce contre
  quoi `AGENTS.md` existe.
