# Une règle sans garde-fou est tenue ou assumée, jamais espérée

> Statut : accepté — 2026-09-11. Complète les ADR 0012 et 0015 d'un
> garde-fou chacun, étend le Gel de l'ADR 0031 d'un axe — la concordance du
> SDK — et corrige la doctrine du SDK que `CLAUDE.md` et `docs/qualite.md`
> portaient depuis l'ADR 0028. Complété par l'ADR 0038 — la relecture qui
> tient les règles assumées a un lecteur.

L'allègement de `CLAUDE.md` (ADR 0034) était un audit de couverture
doctrinale : chaque ligne qu'on hésitait à retirer désignait une règle que
rien ne tenait. Sa sortie n'était pas seulement un fichier plus court, c'était
cette liste — sept angles morts dans `docs/qualite.md`, dont quatre déjà
assumés (le Style Rider, les licences, la couverture, le RED observé) et
**quatre en suspens**, que personne n'avait tranchés :

1. les erreurs métier sont des exceptions, **pas un `Result`** ;
2. les paramètres d'une `DomainException` sont des **primitives, jamais un
   value object** ;
3. **une commande ne traverse pas deux modules** ;
4. **le SDK vient de mise**, jamais du PATH.

Une règle en suspens est la pire des trois : ni tenue, ni assumée. Elle vit
dans `CLAUDE.md` avec sa justification, dilue le reste, et chaque relecture
la redécouvre. Ce que cet ADR décide, c'est le sort de chacune — et le
principe qui empêche la liste de se reformer.

## La décision

> Une règle sans garde-fou est **tenue** — un test rougit — ou **assumée** —
> un angle mort écrit, avec son pourquoi. Jamais espérée.

Le tri s'est fait sur les faits, pas sur la difficulté pressentie : deux des
quatre règles étaient déjà tenues à moitié, et la troisième, donnée pour
« élevée », tenait en un sweep de constructeurs.

### 1. Le `Result` — tenu par `DomainConventionTest`

Le repo n'a aucun `Result` dans un Domain, et ses seuls `Result` sont ceux
des queries, en Application — c'est la forme de l'ADR 0012. Une IA qui
« met du `Result` » le fait par deux routes, et chacune a sa garde :

- **par un paquet** (FluentResults, ErrorOr, OneOf, CSharpFunctionalExtensions) :
  un Domain ne référence rien d'autre que le runtime et la racine du repo —
  le socle et ses Contracts. Aucun `.Domain.csproj` n'avait de
  `PackageReference` ; c'est maintenant une règle ;
- **par un type maison** : aucun type d'un Domain ne se nomme `*Result`,
  `*Error`, `*Outcome`, `Either*`, `Maybe*`, `OneOf*`. Une heuristique de
  nom, assumée comme telle : le Domain n'a aucun usage légitime de ces mots.

### 2. Les primitives — tenues **par construction**

Le constructeur de `DomainException` refuse toute valeur qui ne sérialise
pas en scalaire JSON : primitifs, `string`, `decimal`, `Guid`, dates et
heures, enum. Un value object, un `null`, un objet quelconque lèvent une
`ArgumentException` qui nomme le paramètre et renvoie à `docs/erreurs.md`.

Par construction plutôt que par test, parce que la règle vit alors au même
endroit que le code qui dérive `Code` du nom de la classe, en une seule
ligne de doctrine, et rougit à la **première instanciation** — dans
l'`ExceptionCodesTest` du module, dans tout test de use case, et en
production si une exception jamais testée passait un VO. Ce dernier cas est
un bug de programmation : il doit être bruyant, pas silencieusement
sérialisé en `{ "amount": 20, "currency": "EUR" }`.

L'enum passe **tel quel**, en nombre : la garde ne convertit rien, l'hôte
n'a pas de `JsonStringEnumConverter`, et aucun paramètre du repo n'est un
enum. Le jour où l'un l'est, c'est la sérialisation qui se décide, pas la
garde.

### 3. La traversée — tenue par deux sweeps

`ModuleCompositionTest` tenait déjà « on ne référence que les `Contracts`
du voisin » (ADR 0015). Il restait deux trous, chacun fermé par une garde :

- **Un handler sous scope qui lit chez le voisin.** Un handler de commande —
  ou de domain event, dispatché dans la même transaction (ADR 0003) — qui
  injecte le port de lecture publié d'un autre module ouvre une deuxième
  connexion sous le `TransactionScope` de sa commande. Selon l'ordre
  d'ouverture, Npgsql réutilise le connecteur et la commande passe, ou en
  enrôle un second et la transaction escalade en distribué, non supportée
  hors Windows. C'est le cas non déterministe que `CLAUDE.md` décrivait
  sans pouvoir le tenir. `ApplicationConventionTest` le voit par les
  paramètres de constructeur — le sweep qu'il faisait déjà pour les
  repositories : ce qui tourne sous le scope ambiant ne dépend d'aucun
  `Contracts` d'un autre module. Les queries et les handlers d'integration
  events, hors scope, restent libres. Ce qu'une commande sait du voisin lui
  vient par un integration event, jamais par une lecture sous scope.
- **Un port publié qui écrit.** Rien n'empêchait de déclarer dans un
  `Contracts` une interface avec un `DebitAsync`. `ModuleCompositionTest`
  exige que toute implémentation d'une interface publiée dérive de
  `ModuleReader` — qui refuse matériellement toute row à clé, et ne peut
  donc pas écrire. Les deux canaux inter-modules sont l'event et la lecture
  (ADR 0014, 0015) ; la lecture est maintenant lecture par construction.

### 4. Le SDK — la doctrine était fausse, la garde existait

`CLAUDE.md` disait : « rien ne rattrape l'oubli, le mauvais SDK compile,
puis diverge ». C'est faux à la bande près : `backend/global.json` pinne
`10.0.400` avec `rollForward: latestPatch`, et **tout host dotnet**, mise ou
PATH, refuse un SDK d'une autre bande de fonctionnalités — il ne compile
pas, il échoue. Le seul cas où « le mauvais SDK compile » est un patch
différent de la même bande, et là rien ne diverge. `global.json` était la
garde depuis le début ; la doctrine ne le savait pas.

Le vrai trou était dans l'autre sens : `backend/mise.toml` disait
`dotnet = "10"`, et le jour où un runner neuf installe un `10.0.5xx` frais,
`global.json` le refuse et la CI rougit sans qu'aucun fichier n'ait changé.
Le pin mise devient exact, et le Gel gagne un axe : `QualityGateFreezeTest`
exige que `mise.toml` et `global.json` nomment la même version. Les deux
montent d'un même geste.

La ligne « le SDK vient de mise » reste dans `CLAUDE.md`, en une ligne et
sans justification : c'est un **geste** — `mise exec -- dotnet` — qu'aucune
Porte ne dicte au moment où on tape la commande, pas une règle que rien ne
tient.

### Ce qui est assumé

Une `DomainException` qui n'est instanciée par aucun test ne rencontre la
garde des primitives qu'en production. Un sweep pourrait exiger un
`[TestOf]` par exception ; ce serait une garde sur les tests, pas sur le
code, et la skill `nouvel-agregat` dicte déjà l'`ExceptionCodesTest`. Angle
mort écrit dans `docs/qualite.md`, avec ce pourquoi.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Un Domain ne référence aucun paquet ; aucun type d'un Domain ne se nomme comme un `Result` | `DomainConventionTest` |
| Les paramètres d'une `DomainException` sont des scalaires, jamais `null` ni un VO | le constructeur de `DomainException` (`ArgumentException`), éprouvé par `DomainExceptionTest` ; rougit dans l'`ExceptionCodesTest` du module |
| Un handler de commande ou de domain event ne dépend d'aucun `Contracts` d'un autre module | `ApplicationConventionTest` |
| Une implémentation de port publié dérive de `ModuleReader` | `ModuleCompositionTest` |
| `backend/mise.toml` et `backend/global.json` nomment le même SDK | `QualityGateFreezeTest` (le Gel, ADR 0031) |
| Le SDK d'une autre bande de fonctionnalités | `global.json` — le host dotnet refuse de compiler |
| Une exception jamais instanciée par un test passe un VO en production | **rien** — assumé, voir ci-dessus |
| Un `Result` maison nommé hors de l'heuristique (`Reply`, `Answer`…) | **rien** — la relecture ; l'heuristique tient les noms que le motif porte |

## Options écartées

- **Tester les primitives dans chaque `ExceptionCodesTest`** plutôt que
  dans le constructeur : trois copies du même sweep, une par module, et une
  quatrième à écrire par le cloneur — là où le constructeur tient la règle
  une fois, pour tous.
- **Exiger un test par `DomainException`** : une garde sur les tests, pas
  sur le code ; la recette de `nouvel-agregat` le dicte déjà.
- **Interdire aux agrégats les méthodes qui rendent `bool`** pour bloquer
  `TryWithdraw` : interdit aussi les prédicats. Trop large pour ce que ça
  rattrape.
- **Autoriser la lecture publiée sous scope quand les connexions partagent
  la même chaîne** : c'est le cas du template, où Npgsql réutilise le
  connecteur — mais un cloneur qui sépare ses bases découvrirait l'escalade
  en production. La règle est d'architecture, pas de configuration.
- **Un step CI qui compare `dotnet --version` au `global.json`** : c'est
  exactement ce que le host fait à chaque commande. La garde qui manquait
  était entre les deux pins, pas entre le pin et le binaire.
- **Un sous-agent de relecture** pour les règles qu'aucun test ne sait
  formuler : c'est #32, et il reste nécessaire pour le reste de la doctrine.
  Ici, les quatre règles se testaient.
