# Le DbSetup en scénario différé

> Statut : accepté — 2026-09-10. Amende le point « `DbSetup` fluent et
> bloquant » des options écartées de l'ADR 0002 : le fluent revient, le
> blocage non.

L'ADR 0002 avait tranché le `DbSetup` d'un module en méthodes async, un
`await` par ligne — le fluent d'alors bloquait en `.Result` sous le
`TransactionScope` ambiant, et l'`AggregateException` sur un arrange qui
échoue était un vrai coût. Deux modules d'exemple plus tard, la forme
montrait ses limites : un geste par agrégat à paramètres primitifs nommés
(`CreateBankAccountAsync(iban, currency, balance)`) où chaque propriété
ajoutée à une commande est un paramètre de plus, et où `balance` cachait
un dépôt ; l'id du dernier créé recopié dans chaque test pour le passer au
geste suivant ; et chez le consommateur (Ledger), des helpers privés de
fixture qui jouaient les handlers d'integration event — un setup qui ne
disait pas son nom. Le modèle visé est celui d'un projet voisin : un
scénario qui se lit d'une traite, où un geste comble lui-même le prérequis
qu'on ne lui donne pas.

## La décision

Le `DbSetup` d'un module est un **scénario différé** : chaque geste
enregistre une étape et renvoie le setup, un terminal les rejoue.

```csharp
await DbSetup
    .CreateBankAccount(account => account.WithCurrency("USD"))
    .Deposit(deposit => deposit.WithAmount(50m))
    .RunAsync();

var account = await DbSetup.GetBankAccountAsync();
```

- **La mécanique vit dans `DbSetupBase`** (`SharedKernel.Test.Infrastructure`) :
  `Enqueue(name, step)` empile une étape nommée ; `RunAsync()` — un `Task`,
  pour qu'un `await` oublié soit un CS4014, donc une erreur de build, là où
  un awaitable maison oublié passerait en silence — les exécute dans
  l'ordre, vide la file, et laisse le setup réutilisable pour un second
  scénario dans le même test (un autre Instant, par exemple). Le builder se
  configure et le prérequis se comble **à l'exécution**, pas à
  l'enregistrement : « le dernier créé » est celui de l'étape précédente.
- **Un échec d'étape est enveloppé** : `InvalidOperationException("Étape 2
  (Deposit) du DbSetup a échoué : …", inner)`. Un arrange qui rate se
  distingue d'un act qui rate au premier coup d'œil dans la sortie NUnit,
  le rang dit la ligne, l'inner garde le type et le code.
- **Les accesseurs sont gardés** : `GetLastXxxId()`, `GetXxxAsync()`
  passent par `Arranged(read)`, qui lève tant qu'une étape attend — une
  lecture avant le `await` ne rend pas un état incomplet en silence. Et
  `BaseIntegrationTest<TFactory, TDbSetup>` rougit au TearDown si le test
  finit avec des étapes en attente : un geste sans `RunAsync()` n'est pas
  un `Task` oublié, c'est un setup qui a arrangé moins que le test ne lit.
- **Un builder par entrée du module**, dans son `Test.Infrastructure/Builders/` :
  `<Commande>Builder` chez un publieur (`OpenBankAccountCommandBuilder`,
  `DepositMoneyCommandBuilder`…), `<Event>Builder` chez un consommateur
  (`MoneyDepositedIntegrationEventBuilder`) — ses entrées sont les
  integration events qu'il consomme. Champs privés à défauts valides,
  `With<Propriété>()`, le prérequis en **nullable** exposé en lecture et
  posé par `Of(id)`, `Build()` qui lève s'il manque encore. Le geste fait
  `builder.Of(…)` quand il est nul, depuis le dernier créé — ou en crée un
  par défaut s'il n'y en a aucun.
- **Un geste = une entrée.** `CreateBankAccount`, `Deposit`, `Withdraw`,
  `Close` chez Bank ; `CreateBankAccount`, `RecordDeposit`,
  `RecordWithdrawal` chez Ledger — les deux derniers jouent le handler
  d'integration event lui-même, le vrai use case d'écriture du module,
  hors outbox (le chemin complet est prouvé par son `CqsContractTest`).
  Pas de raccourci composé : le chaînage existe pour que le scénario dise
  ce qu'il fait.
- **Le consommateur réutilise les builders du publieur** par une référence
  de test à test (`Ledger.Test.Infrastructure` → `Bank.Test.Infrastructure`,
  commentée dans le csproj) : NUnit ne découvre que les fixtures de
  l'assembly cible. Il garde son propre geste et sa propre file d'étapes,
  et ne prend de Bank que l'id.
- **Les accesseurs rendent l'état arrangé** : l'id, et l'agrégat lui-même
  par le repository du module — pour un arrange, ou pour affirmer ce que
  le Domain a enregistré (l'Acteur, l'Instant) ; le cas nominal d'une
  commande reste vérifié par une query (skill `nouvelle-commande`). Rien
  d'autre tant qu'un test ne le demande pas.
- **Acteur et Instant restent hors du scénario** : le test les pose sur les
  fakes avant l'arrange, comme avant ; deux Instants, deux `RunAsync()`.
- **Vocabulaire** : gestes au présent nu, sans suffixe `Async` — ils
  renvoient le setup, rien n'est attendu à ce point-là ; c'est le seul
  endroit du repo où une méthode sans `Async` précède de l'E/S, et c'est
  dit ici. Le terminal est `RunAsync()`. Un accesseur qui lit la base
  garde son `Async` : c'est un `Task`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Ordre d'exécution, prérequis résolu à l'exécution, file vidée et réutilisable, enveloppe d'échec, garde des accesseurs | `DbSetupBaseTest` (SharedKernel.Test.Infrastructure, sans hôte ni base) |
| Un `RunAsync()` non attendu | le build : CS4014, warning tenu en erreur |
| Un scénario jamais joué | le TearDown de `BaseIntegrationTest<TFactory, TDbSetup>` |
| Le module de référence emprunte la forme | ses tests d'intégration : tous en scénarios |

## Options écartées

- **Un `await` par ligne, sans chaînage** (l'ADR 0002) : le prérequis
  implicite et les builders auraient pu s'y ajouter, mais l'id du dernier
  créé resterait à recopier entre deux gestes, et un scénario de quatre
  étapes prendrait huit lignes qui ne se lisent pas d'une traite.
- **Des extensions sur `Task<DbSetup>`** : chaînage sans terminal, mais
  chaque geste existerait deux fois (sur le setup et sur la tâche), et
  l'échec d'une étape n'aurait ni rang ni nom.
- **Un `GetAwaiter()` sur le setup** plutôt que `RunAsync()` : plus court
  d'un appel, mais un awaitable maison oublié ne déclenche aucun
  avertissement — un `Task` oublié en déclenche un, tenu en erreur.
- **`Guid.Empty` en sentinelle du prérequis** (le projet voisin) : une
  valeur qui part vraiment dans la commande quand le setup ne la remplace
  pas, et qui échoue plus loin en 404 au lieu de dire ce qui manque.
- **Les builders dans l'Application** (le projet voisin) : visibles de
  partout sans rien référencer, mais du code de test dans l'assembly de
  production. **Un 7ᵉ projet `Test.Support`** par module : propre, mais un
  projet de plus dans un template qui en a déjà six, pour deux classes.
- **Réutiliser le `DbSetup` de Bank entier chez Ledger**, par dérivation ou
  composition : tous les gestes et accesseurs de Bank, mais deux files
  d'étapes à réconcilier, et un consommateur qui exerce le publieur par son
  outillage de test plutôt que par ses commandes.
- **Lire l'état par la query du module** plutôt que par le repository : le
  test resterait sur la surface publique, mais l'arrange dépendrait d'une
  lecture qui doit exister et qui a sa propre logique (404, projection).
- **`As(actor)` / `At(instant)` en étapes du scénario** : possible, mais
  aucun test n'en a besoin, et la restauration après `RunAsync()` est une
  sémantique de plus à porter. Une issue à part si un scénario
  multi-acteurs apparaît.
- **Un test de convention** (toute commande a son builder, tout geste vit
  dans une partielle nommée par son agrégat) : un builder manquant se voit
  au premier test qui en a besoin ; de la doctrine sur du code de test.
