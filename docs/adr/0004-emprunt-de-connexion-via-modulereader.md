# Emprunt de connexion via ModuleReader

Écrire le deuxième reader du codebase demandait de ré-encoder à la main cinq
invariants de cycle de vie de connexion, tous en commentaires et aucun dans un
type — dont un (ouvrir sa propre connexion) dont la violation escalade la
transaction ambiante en distribué, non supporté hors Windows. On les absorbe
dans une base `ModuleReader` (`LoreBank.SharedKernel.Infrastructure`) : un
reader concret ne fournit que son SQL, un dictionnaire de paramètres à clés
nues et sa lecture de colonnes. La base est écrite en ADO.NET générique
(`DbCommand.CreateParameter`, pas de cast Npgsql) : rien dans l'emprunt n'est
spécifique à un provider, et c'est ce qui la rend testable en unitaire sur
Sqlite (`ModuleReaderTest` — emprunt refcompté, fermeture dans le `finally`
même quand le SQL lève). `GetBankAccountByIdTest` reste la preuve Npgsql
réelle, par reader, du lien colonne → propriété.

## Options écartées

- **Méthode d'extension sur `DbContext` plutôt qu'une base** : la
  découvrabilité reposerait sur « savoir que l'extension existe » ; la base
  est le geste symétrique de `ModuleDbContext`, même vocabulaire pour un
  cloneur.
- **Callback `Action<DbCommand>` pour les paramètres** : plus flexible, mais
  ré-expose l'asymétrie `@id` (SQL) / `"id"` (paramètre) qu'on cherche à
  enterrer. Le dictionnaire suit la forme des paramètres de `DomainException`.
- **Variante liste dès maintenant** : le seul reader existant lit 0..1 ligne —
  un adapter = seam hypothétique. `QueryListAsync` s'écrira sur le même cœur
  quand un vrai reader la demandera.
