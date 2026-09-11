# Les Signaux, de l'outbox à l'écran

Comment le front est prévenu qu'un fait a été livré sans rien deviner de
son contenu — et pourquoi le socle ne pousse jamais un état. Décision et
alternatives : ADR 0026.

## En une phrase

Un integration event qui implémente `ISignalsClients` nomme sa ressource ;
une fois sa ligne d'outbox livrée à tous ses handlers, chaque instance de
l'hôte pousse un Signal nu — quoi, où, quand — à ses clients abonnés sur
`GET /api/signals`, en SSE ; le client refait son `GET`.

## Le chemin

```
commande HTTP ──► domain event ──► handler de mapping ──► ligne d'outbox
                                                            (resource_kind, resource_id)
OutboxDispatcher, chaque seconde :
  1. OutboxProcessor : livre la ligne à ses handlers, la marque livrée
  2. SignalTailer    : relit les lignes livrées depuis son curseur,
                       pousse un Signal par ligne qui porte une ressource
SignalHub ──► chaque abonné dont le filtre et la policy admettent le Signal
SignalsController ──► `data: {"discriminant":…,"resourceKind":…,"resourceId":…,"occurredAt":…}`
front ──► relit son GET
```

Un Signal arrive donc **après** que toutes les conséquences in-process du
fait sont en base — la ligne du Ledger comprise — et jusqu'à deux secondes
après le commit de la commande (une passe pour livrer, une pour suivre).
Un handler qui échoue retarde le Signal jusqu'au retry qui réussit ; une
ligne poison ne signale jamais.

## Côté module : marquer l'event

```csharp
[IntegrationEvent("bank.money-deposited")]
public sealed record MoneyDepositedIntegrationEvent(
    Guid AccountId,
    decimal Amount,
    string Currency
) : ISignalsClients
{
    public string ResourceKind => "bank-account";

    public Guid ResourceId => AccountId;
}
```

C'est tout. `ResourceKind` est un nom stable choisi en kebab-case — la
grille du discriminant, jamais un nom de type .NET — validé par le
publisher à l'écriture de la ligne (`SignalResource.Of`). Le payload
d'outbox ne le porte pas : la ressource vit dans ses deux colonnes, et le
suiveur n'a aucun catalogue à tenir. Signaler commence par publier : un
`ISignalsClients` sans `[IntegrationEvent]` rougit dans
`ModuleCompositionTest`.

## Côté client : le flux

`GET /api/signals`, `text/event-stream`. Le Signal est le `data:` de chaque
event, en JSON camelCase — le schéma `Signal` de la Description, dont le
Client tire son type :

```
: keep-alive

data: {"discriminant":"bank.money-deposited","resourceKind":"bank-account","resourceId":"0f7d…","occurredAt":"2026-09-09T12:00:00+00:00"}

```

- **Filtre** : `?resource=<kind>/<guid>`, répétable ; absent, tout passe.
  Un filtre mal formé est un `422 INVALID_SIGNAL_RESOURCE`, la forme
  d'erreur du socle (`docs/erreurs.md`).
- **Ni `event:` ni `id:`** : un `EventSource` n'a pas d'écouteur générique
  pour les events nommés — `onmessage` reçoit tout, le discriminant est
  dans le Signal — et il n'y a pas de rejeu sur `Last-Event-ID` : un client
  reconnecté relit ce qu'il affiche.
- **Keep-alive** : un commentaire SSE à l'ouverture — c'est le premier
  octet du corps qui met les en-têtes sur le fil, Kestrel comme un proxy
  Node les retiennent jusque-là — puis toutes les `Signals:KeepAliveSeconds`
  (15 par défaut) quand rien ne passe, pour les proxies.
- **Client lent** : le canal d'un abonné est borné (64) ; s'il déborde, le
  socle ferme le flux en `LogWarning`, le navigateur reconnecte, le front
  relit. Aucun Signal n'est perdu en silence.
- **Multi-instance** : chaque instance suit toutes les outbox avec son
  propre curseur, né à son démarrage. Un client voit ce qui est livré par
  n'importe quelle instance ; il ne voit pas ce qui a été livré avant que
  la sienne démarre.

Le front ouvre **un** `EventSource` par onglet, non filtré
(`SignalsProvider`), et chaque composant s'abonne à la ressource qu'il
affiche (`useSignals("bank-account", accountId, refresh)`). La relecture
immédiate après sa propre commande reste : le Signal ne remplace pas le
CQS, il couvre ce que le client ne pouvait pas savoir.

## Autorisation

Le socle n'autorise rien (ADR 0023). Le filtre est un choix du client, pas
une garde : sans policy, tout client connecté reçoit tous les Signaux des
events opt-in. Le port `ISignalPolicy` (`Actor` + `Signal` → `bool`) est
consulté au fan-out avec l'Acteur de la connexion ; l'implémentation du
socle laisse tout passer. Le cloneur qui a des données par utilisateur
enregistre la sienne dans l'hôte, après `SharedKernelInfrastructureModule`
— la dernière inscription Autofac gagne — et son `[Authorize]` s'applique
au `GET` comme à toute route.

## Observabilité

Sur le Meter `LoreBank.Signals` (ADR 0022, exporté par la Télémétrie de
l'hôte — ADR 0025) : `lorebank.signals.subscribers`, les abonnés connectés
à cette instance, et `lorebank.signals.delivered`, les Signaux poussés. Pas
de trace par Signal : le fait est déjà tracé par sa livraison.

## Ce que les tests garantissent

- `SignalResourceTest`, `SignalFilterTest`, `SignalHubTest`,
  `SignalStreamResultTest`, `SignalOptionsTest`
  (`LoreBank.SharedKernel.Test.Unit`) : le format du genre, le filtre, le
  fan-out, la policy, le débordement, les métriques, le fil SSE et le
  keep-alive, la fin propre quand le client part.
- `SignalTailerTest` (`…Test.Infrastructure/Hosting/`, sur Probe) : rien
  tant que la ligne est en attente ou qu'un handler échoue, un Signal par
  ligne livrée quel que soit le nombre de passes, rien pour un event sans
  marqueur, deux instances aux curseurs indépendants qui signalent toutes
  deux.
- `SignalContractTest` (`Apis/`) : le flux en HTTP — media type,
  keep-alive, Signal reçu après livraison et suivi, filtre, policy, 422.
- `DescriptionContractTest` : `/api/signals` en `text/event-stream` sur le
  schéma `Signal`, `operationId` `Signals_Subscribe`.
- `ModuleCompositionTest` : tout `ISignalsClients` des Contrats d'un module
  monté porte `[IntegrationEvent]`.
- `SignalPublicationTest` (Bank) : un dépôt en HTTP, une passe, un suivi —
  le client abonné au compte reçoit `bank.money-deposited`, sans montant.
- `IntegrationEventPublicationTest` (Bank) : la ligne d'outbox porte la
  ressource, le payload ne la porte pas.

Le harnais expose `SignalProbe` (`OpenAsync` sur le flux, `TailAsync` pour
piloter le suiveur — qui s'amorce à son premier passage : un test amorce
avant d'agir) et `ConfigurableSignalPolicy`, la policy pilotée par les
tests, effacée par `ResetFakes`.
