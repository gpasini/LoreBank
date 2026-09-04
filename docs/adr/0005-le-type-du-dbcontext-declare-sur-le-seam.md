# Le type du DbContext déclaré sur le seam

Rouvre en partie l'ADR 0001, qui avait écarté « `Type DbContextType`
générique ». Le coût de l'absence s'était accumulé : le harnais de test avait
besoin du fait quand même et le retrouvait par scan d'assembly
(`ModuleDbContexts`), une convention non écrite — « ton DbContext vit dans la
même assembly que ton Module Autofac » — qu'aucun seam n'énonçait ; et
`ConnectionRedirectTest`, gardien de l'échec « silencieux et destructeur »
qu'ADR 0002 existe pour prévenir, passait au vert sans rien affirmer si ce
scan ne trouvait rien. `IHostModule` déclare donc `Type DbContextType` — le
singulier encode la doctrine « un DbContext par module », la vacuité meurt
structurellement — et `MigrateAsync` disparaît du seam : son corps était du
pass-through dérivable du type, l'hôte migre désormais via lui. La raison
d'ADR 0001 (« chaque module garde la main sur sa configuration de
persistance ») ne portait que sur `ConfigureDbContext`, qui reste.

## Options écartées

- **`IReadOnlyList<Type>`** : fidèle au comportement du scan, mais garde la
  liste vide possible et réduit la doctrine à un message d'assertion.
- **Garder `MigrateAsync`** : le seam aurait porté deux fois la même
  information — le type, et un hook qui ne fait que l'utiliser.
