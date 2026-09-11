# Le test avant le code : le RED observé, jamais supposé

> Statut : accepté — 2026-09-11 ; étend le Gel de l'ADR 0031 d'un axe — le
> résidu d'un cycle interrompu — et corrigé par l'ADR 0033 : le hook qu'il
> renvoyait à l'issue #29 n'entre pas dans la réserve des hooks.

Le mot TDD n'apparaissait nulle part dans ce repo : ni dans `CLAUDE.md`, ni
dans `docs/`, ni dans les neuf skills. Et l'ordre des recettes disait le
contraire de l'intention : dans les six qui ont une étape de test, elle était
la dernière ou l'avant-dernière — `nouveau-value-object` 7 sur 7,
`nouveau-domain-event-handler` 5 sur 5, `nouvel-agregat` 9 sur 10,
`nouvelle-commande` 5 sur 8, `nouvelle-query` 7 sur 9,
`nouvelle-data-migration` 4 sur 5. Le chapeau `ajouter-fonctionnalite`
demandait bien de ne pas « reporter les tests à la fin » ; l'ordre des étapes
le démentait.

C'est cet ordre qui produit l'échec-type de l'agent. Un test écrit après le
code est **taillé sur le code qu'il vient d'écrire** : il confirme
l'implémentation au lieu de la spécifier, et passe à côté du cas que
l'implémentation a oublié — puisque c'est elle qui lui a dicté sa liste de
cas. Aucune Porte ne peut le voir : le test est vert, la Couverture monte, et
le filet a un trou de la forme exacte du bug.

## La décision

**Le squelette, puis le test, puis le corps** — et le rouge se voit entre les
deux.

1. Le squelette minimal qui compile : signatures, corps vides (`throw new
   NotImplementedException()`), plus ce que le test doit nommer pour compiler
   — l'exception d'un value object, l'id typé d'un agrégat.
2. Le test, écrit maintenant, contre ce vide.
3. **Le RED observé.**
4. Le corps, jusqu'au vert.

Un seul régime, pour toutes les briques. Le régime strict — « le test
d'abord, quitte à ne pas compiler » — a été écarté pour une raison technique
et non de confort : **en C#, un test qui ne compile pas n'est pas un RED,
c'est une absence de signal.** La solution entière cesse de compiler, plus
rien ne tourne, et l'agent lit `CS0246` au lieu de « l'assertion échoue ». Et
dès qu'on admet le squelette, la distinction par brique se dissout : du value
object à la commande, seule change la *quantité* de squelette — une classe
d'un côté, record + handler + action de l'autre — jamais la méthode.

### Ce qu'est un RED

Le test **compile** et **échoue sur son assertion**, ou sur le
`NotImplementedException` du squelette. Jamais sur une erreur de
compilation ; jamais contre un squelette qui contient déjà l'implémentation.
C'est la définition qui porte tout le reste : un RED supposé ne prouve rien.

### Un test qui spécifie, un test qui épingle

Tous les tests ne se placent pas au même endroit, parce qu'ils ne font pas le
même travail.

Un test qui **spécifie** dit ce que le code doit faire : il s'écrit avant, et
son rouge est la preuve qu'il porte une exigence que rien ne satisfait
encore.

Un test qui **épingle** photographie un contrat existant — l'ensemble exact
des clés JSON d'une route, le diff de `backend/openapi/lorebank.json` — pour
faire rougir une dérive *future*. Il ne peut pas précéder ce qu'il
photographie, et le RED-first ne lui veut rien dire. Il reste à la fin de la
recette.

Concrètement, seules deux skills portent les deux natures :
`nouvelle-commande` (le test du use case remonte ; `CqsContractTest` et la
relecture de la Description restent) et `nouvelle-query` (le test du use case
remonte ; les clés JSON et la Description restent).

### Une seule exemption

`nouvelle-migration-schema` : il n'y a aucun test à écrire. La preuve est
`mise run migrate` sur le poste et la suite d'intégration, qui rejoue la
timeline entière sur un conteneur vierge. L'exemption est **écrite** dans la
recette — une exemption tacite serait un trou, une exemption écrite n'en est
pas un.

Deux cas qu'on croyait particuliers n'en sont pas. `nouvelle-data-migration`
suit la règle mot pour mot : classe avec `ExecuteAsync` vide, test de rejeu
sur données arrangées, rouge, corps. Et `nouveau-module` a son rouge **déjà
latent** dans l'ordre actuel de sa recette : déclarer le module dans
`HostModules.All` avant d'écrire sa chaîne de connexion casse la composition,
parce qu'`AddModuleDbContext` refuse une clé absente (ADR 0008). La recette
n'avait qu'à le nommer — et à dire le mécanisme, sans quoi un agent prend le
rouge pour un bug.

### La preuve, et sa limite

Un RED observé **ne laisse aucune trace dans le diff**. La sortie du run
rouge est la seule preuve, et elle est volatile.

Le récap de `ajouter-fonctionnalite` la fixe : une section « RED observés »,
un tableau brique / test / message d'échec, qui se réduit à une ligne quand
il n'y a qu'une brique. L'issue est déjà la mémoire du chantier ; elle
devient aussi celle de la méthode.

Sa limite est dite franchement : **le récap ne couvre que ce qui passe par le
chapeau**. Une brique déroulée seule, hors issue, ne laisse rien. C'est un
cran au-dessus de l'espoir, ce n'est pas une Porte — et `docs/qualite.md` le
recense pour cela dans « Ce qu'aucune porte ne tient ». Rien ne viendra la
fermer : l'ADR 0033 a écarté le hook qui l'aurait fait, parce qu'il aurait
porté une règle au lieu d'un moment.

### Le résidu

Cette décision instruit d'écrire un `NotImplementedException`, construit dont
le repo avait zéro occurrence. Le Gel (ADR 0031) gagne donc un axe
d'interdiction nue : zéro `NotImplementedException` dans le code commité. La
suite le tient dans le cas courant — le test qu'on vient d'écrire est rouge —
mais pas sur un chemin qu'aucun test ne traverse. Ouvrir une porte au moment
où l'on en ferme une n'aurait pas eu de sens.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Un squelette laissé derrière le RED | `QualityGateFreezeTest` (le Gel, ADR 0031) |
| Le RED lui-même | **rien** — c'est une méthode, pas une Porte. Le récap de l'issue en porte la trace, `docs/qualite.md` le recense parmi les angles morts assumés |

## Options écartées

- **Le régime strict, « le test d'abord quitte à ne pas compiler »** : en C#
  il ne produit pas un rouge mais un silence. Voir ci-dessus.
- **Un régime par brique** — strict dans le Domain, pragmatique au-dessus :
  une doctrine à deux étages à retenir, pour une distinction qui disparaît
  dès qu'on admet le squelette.
- **Une dixième skill** qui porterait la méthode : les neuf sont des
  « nouveau X », déclenchées par un geste de construction ; une
  skill-méthode entrerait en concurrence avec elles à chaque invocation. Et
  les skills TDD du marché sont des plugins du poste — un cloneur ne les a
  pas, la méthode doit vivre dans le repo.
- **Une ligne « rien ne le tient » dans les huit tables Garde-fous des
  skills** : huit répétitions d'un vide, quand le repo a déjà un registre de
  ce qu'aucune Porte ne tient.
- **Un hook qui vérifie qu'un rouge a précédé le vert** : c'est le seul
  mécanisme qui fermerait la limite du récap, et il a été renvoyé à l'issue
  #29. **L'ADR 0033 l'a écarté** : un hook n'est admis que s'il impose le
  *moment* d'un garde-fou existant — lancer une Porte, refuser un fichier
  généré, rappeler une skill. Or aucune Porte ne tient le RED ; un hook qui
  l'imposerait ajouterait une règle que rien ne rejoue hors de Claude Code.
  Le RED reste donc sans garde-fou mécanique, et `docs/qualite.md` le recense
  comme tel — c'est la limite, écrite, de cette décision.
- **Restructurer chaque recette en trois phases** (Squelette / Test /
  Corps) : la liste d'artefacts d'une recette sert aussi de checklist de ce
  qui doit exister ; la redistribuer en trois blocs la détruit, et réécrit
  les neuf skills en entier pour un gain d'ordre que la remontée du test
  obtient seule.
