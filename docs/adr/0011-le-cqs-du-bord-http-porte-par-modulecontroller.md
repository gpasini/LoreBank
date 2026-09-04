# Le CQS du bord HTTP porté par ModuleController

La règle « une action qui mute ne renvoie aucune représentation » était une
discipline écrite dans chaque controller : un `ExecuteAsync` privé recopié
par controller — typé `IRequest`, donc rien n'empêchait d'y passer une query
et de servir un 204 à une lecture — et le bloc `CreatedAtAction(...,
value: null)` recopié par création. Sa seule preuve (`CqsContractTest`)
vivait dans le module d'exemple, que le cloneur supprime. On absorbe : une
base `ModuleController` (`LoreBank.SharedKernel.Api`) porte `[ApiController]`
(hérité — une annotation de moins à recopier) et deux méthodes protégées
typées par les marqueurs du CQS : `SendAsync(ICommand)` → 204,
`CreateAsync(ICreationCommand, actionName)` → 201 + `Location` + corps vide.
La règle devient un fait du système de types — une query ne peut emprunter
aucun des deux chemins. Pas de `QueryAsync` : ce serait un pass-through, les
lectures gardent leur `Sender.Send` — et servent le `Results/` de leur query
tel quel (ADR 0012). La preuve se
dédouble : `ModuleControllerTest` (unitaire, socle — survit à la suppression
de Bank) épingle la plomberie, `CqsContractTest` (E2E, Bank) continue de
prouver le bout en bout, Location suivi d'un GET.

## Options écartées

- **Méthodes d'extension sur `ControllerBase`** : pas de hiérarchie imposée,
  mais chaque appel traîne l'`ISender` en paramètre et `[ApiController]`
  reste à recopier — et aucun endroit pour typer le geste une fois.
- **Statu quo (discipline + preuve dans le module)** : la discipline tenait,
  mais sa preuve partait avec le module d'exemple — le même raisonnement qui
  a rapatrié `ErrorContractTest` dans le socle.
- **Absorber aussi les lectures (`QueryAsync`)** : interface sans
  implémentation à cacher — le deletion test le rejette.
