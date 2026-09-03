# Socle de test d'intégration partagé

Le harnais d'intégration (Testcontainers, hôte statique, transaction par test,
workaround Autofac) vivait dans `Bank.Test.Infrastructure` : ~130 lignes que
chaque module aurait recopiées, et dont la copie était destructrice — la
factory ne redirigeait que *son* `DbContextOptions`, laissant les DbContext des
autres modules pointer sur la base réelle du développeur, que l'hôte de test
(en Development) migrait au démarrage. On extrait
`LoreBank.SharedKernel.Test.Infrastructure` : factory abstraite qui redirige
**toutes** les `ConnectionStrings:*` vers le conteneur (garantie épinglée par
`ConnectionRedirectTest`), hook `ConfigureModuleContainer`/`ResetFakes` pour
les fakes du module, `TestHost<TFactory>` générique, bases
`BaseHostTest`/`BaseIntegrationTest`. C'est le seul projet SharedKernel à
référencer l'hôte — assumé : un harnais d'intégration teste l'hôte réel.

## Options écartées

- **Rediriger par descripteur `DbContextOptions<T>`** (balayage réflexif de
  l'`IServiceCollection`) : la surcharge de configuration ne connaît ni EF ni
  les modules — « en test, toute chaîne de connexion mène au conteneur ».
- **Découverte des personnalisations par scan d'assembly** : génériques
  explicites (`TestHost<TFactory>`) préférés, cohérents avec l'ADR 0001 — un
  échec de découverte par scan serait silencieux, la maladie qu'on soigne.
- **`DbSetup` fluent et bloquant** (`.Result` sous le `TransactionScope`
  ambiant) : méthodes async — le fluent était cosmétique, l'`AggregateException`
  sur une arrange qui échoue était un vrai coût.
