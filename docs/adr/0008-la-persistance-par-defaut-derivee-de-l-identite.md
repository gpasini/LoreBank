# La persistance par défaut dérivée de l'identité

Prolonge l'ADR 0007. Le corps de `ConfigureDbContext` faisait trois lignes,
mais son interface en exigeait quatre invariants, tous hors des types : la clé
sous `ConnectionStrings` précisément (la seule section que le harnais redirige
— ADR 0002), une clé absente rendant un `null` qui voyage jusqu'au premier
SQL, le provider Npgsql, et jamais `Enlist=false` — dont CLAUDE.md disait
lui-même qu'il ne produit « ni erreur ni avertissement ni test qui échoue ».
On les concentre dans une extension publique
`AddModuleDbContext<TContext>(services, configuration, connectionStringName)`
(`LoreBank.SharedKernel.Infrastructure`) : clé absente ou `Enlist=false`
cassent à la composition avec un message qui nomme l'invariant
(`ModuleDbContextRegistration`, épinglée par `ModuleDbContextRegistrationTest`) ;
le package du provider monte dans le csproj du socle, les modules l'héritent
en transitif. `HostModule<TDbContext>.ConfigureDbContext` devient `virtual`
avec pour défaut `AddModuleDbContext<TDbContext>(configuration,
$"{ModuleName}Db")` — la clé de connexion rejoint les assemblies dans
l'identité dérivée, et le couplage non vérifié entre l'adapter et
`appsettings.json` disparaît. Le membre reste sur le seam `IHostModule` et le
module garde la main (ADR 0001) : une autre clé = surcharge d'une ligne qui
rappelle l'extension ; un provider exotique = surcharge complète, invariants à
la charge du module.

Subtilité tenue par `ConnectionRedirectTest` : la validation est immédiate
(échec à la composition), mais la valeur est **relue** dans la fabrique
d'options — le harnais d'intégration redirige les `ConnectionStrings` après
la composition de `Program.cs`, et une valeur figée à l'enregistrement
enverrait les tests sur la base réelle du développeur, l'échec silencieux et
destructeur qu'ADR 0002 existe pour prévenir.

## Options écartées

- **Helper appelé par chaque adapter** (`ConfigureDbContext` reste abstrait) :
  les invariants sont absorbés mais le littéral de la clé reste saisi à la
  main, et son couplage avec `appsettings.json` reste non vérifié. Le défaut
  dérivé fait tomber les deux.
- **Défaut scellé + hook d'options virtuel** : aucun module ne personnalise
  ses options aujourd'hui — un adapter = seam hypothétique, la règle qui a
  fait écarter `QueryListAsync` dans l'ADR 0004. La surcharge complète de la
  méthode couvre le cas exotique sans inventer de hook.
- **Helper interne** : un module qui surcharge (autre nom de clé) ne pourrait
  plus l'appeler depuis l'hôte et ré-encoderait les quatre invariants à la
  main — la recopie qu'on enterre, recréée au premier module qui dévie.
- **Garder l'avertissement `Enlist=false` en prose** : un invariant dont la
  violation ne fait échouer aucun test n'est pas tenu par de la documentation.
