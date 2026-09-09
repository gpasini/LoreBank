---
name: nouvelle-migration-schema
description: À utiliser dès qu'un changement touche le modèle persisté — propriété d'agrégat ajoutée/renommée, nouveau VO mappé, nouvelle table — ou avant tout `dotnet-ef migrations`. Une migration de schéma se génère par la commande EF, jamais à la main.
---

# Nouvelle migration de schéma

## Principe

Le modèle EF est la source de vérité, la migration en est la **dérivée
générée** : on change le mapping (`IEntityTypeConfiguration`), on lance la
commande, on relit ce qu'elle a produit. Si la migration générée ne dit pas ce
qu'on voulait, on corrige le modèle et on régénère — le fichier de migration
ne s'écrit pas à la main.

## Recette

1. Modifier le modèle : l'agrégat côté Domain, puis son
   `IEntityTypeConfiguration` dans `Persistence/Configurations/` de
   l'Infrastructure — `HasConversion` pour un VO mono-valeur, `OwnsOne` pour
   un VO multi-champs éclaté en colonnes.
2. Générer, depuis `backend/` :

   ```bash
   mise exec -- dotnet tool run dotnet-ef migrations add <NomDeLaMigration> \
     --project LoreBank.<Module>.Infrastructure \
     --startup-project LoreBank.Host \
     --context <Module>DbContext \
     --output-dir Persistence/Migrations
   ```

3. Relire la migration générée et le snapshot : les colonnes, types et
   contraintes attendus, dans le schéma du module — et rien d'autre (un diff
   inattendu signale un modèle mal configuré, pas une migration à retoucher).
   Puis `mise run format` (depuis `backend/`) : le générateur écrit un BOM
   que `.editorconfig` refuse, la porte Format rougirait (ADR 0028).
4. Appliquer : `mise run migrate` (le verbe `migrate` de l'hôte compose les
   modules comme l'API puis sort — le démarrage de l'API ne migre jamais,
   ADR 0006).
5. Un backfill nécessaire entre deux formes du schéma passe par la skill
   `nouvelle-data-migration` : le triptyque ajouter / backfiller / resserrer
   tient en une release, la timeline fusionnée ordonne le tout (ADR 0013).

## Exemple de référence

`backend/LoreBank.Bank.Infrastructure/Persistence/Migrations/` et
`Persistence/Configurations/BankAccountConfiguration.cs`.

## Garde-fous

| Règle | Ce qui rougit si elle casse |
|---|---|
| Aucun écart entre modèle et migrations | `ModuleCompositionTest` (`HasPendingModelChanges`) — un mapping changé sans migration générée rougit là |
| Un schéma PostgreSQL par module, jamais `public` | `ModuleCompositionTest` |
| Les migrations s'appliquent (timeline complète, base vide comprise) | le harnais d'intégration migre son Testcontainer par `ModuleMigrator` — toute la suite `Test.Infrastructure` passe dessus |
| Une migration générée au format du repo (sans BOM) | `mise run format:check` — la porte Format de la CI |

## Pièges

- Une migration déjà commitée est immuable : un raté se corrige par une
  **nouvelle** migration (ou `migrations remove` si elle n'a jamais quitté le
  poste).
- La collection d'events de l'agrégat n'est pas à ignorer dans la
  configuration : EF ne mappe pas une propriété sans setter d'un type non
  mappable (`ModuleDbContextTest` épingle ce contrat).
- Renommer une colonne casse les configs keyless des rows de lecture
  (`Persistence/ReadRows/`, hors migrations — rien ne signale la dérive) et
  le SQL de bordure des data migrations : les tests de relecture
  (`GetBankAccountByIdTest`) rougissent — les faire passer fait partie de la
  migration.
- La chaîne de connexion du poste vit dans `appsettings.json` de l'hôte ; le
  harnais de test, lui, redirige tout vers son conteneur — une migration ne se
  teste jamais sur la base du poste.

## Avant de terminer

Build sans warning, `mise run migrate` passé sur le poste, et la suite
`Test.Infrastructure` verte — elle rejoue la timeline entière sur un conteneur
vierge, ce qu'aucun poste déjà migré ne prouve.
