# Le Signal : le temps réel du socle, nu et après livraison

> Statut : accepté — 2026-09-09.

Le CQS du bord HTTP est strict (ADR 0011) : une commande ne renvoie rien,
le client qui veut l'état d'après fait un `GET`. Ça suffit tant que le fait
qui change l'écran est celui que le client vient de poster. Ça ne suffit
plus pour un fait produit **hors de sa requête** — la ligne comptable que le
Ledger écrit une seconde après le dépôt, par l'outbox — ni pour un fait
produit **par un autre client**. Le front d'exemple le disait par un aveu :
le composant Ledger relisait deux fois puis offrait un bouton.

On décide un canal de push porté par le socle, le **Signal** : un message
nu, poussé aux clients quand un integration event a été **livré** — la
ligne d'outbox marquée livrée par le processor est la notification, rien
d'autre. Il dit *quoi* (le discriminant) et *où* (la ressource : un genre
stable en kebab-case et un identifiant), jamais *comment* : le client
refait son `GET`, il n'existe qu'une représentation, celle de la lecture.

Le canal est **SSE**, natif en .NET 10 (`System.Net.ServerSentEvents`, sans
package) et natif au navigateur (`EventSource`, reconnexion automatique) :
un `GET /api/signals` en `text/event-stream`, porté par un controller du
socle (`SharedKernel.Api`) — la première route du socle côté clients — et
décrit dans la Description (ADR 0019) sur le schéma `Signal`, comme toute
lecture. Un flux global par client, filtré par `?resource=<kind>/<guid>`
(répétable, absent = tout) ; pas d'`id:` ni de rejeu sur `Last-Event-ID` —
un client reconnecté relit ce qu'il affiche ; pas d'`event:` non plus, le
discriminant est dans le Signal — un `EventSource` n'a pas d'écouteur
générique pour les events nommés, et le front n'a aucun discriminant à
écrire pour tout recevoir ; un commentaire de keep-alive à intervalle fixe
pour les proxies.

La **source** est l'outbox, après livraison. Chaque instance de l'hôte suit
ses outbox (`SignalTailer`) : à chaque passe de l'`OutboxDispatcher`, après
la livraison, elle lit les lignes marquées livrées depuis son curseur —
un par module, initialisé au démarrage — et les pousse à ses abonnés. Le
multi-instance (ADR 0021) est couvert sans connexion longue ni nouvelle
pièce : ce qu'une instance a livré, toutes le voient dans la table. Le
prix est une seconde de latence de plus, celle du polling.

L'**opt-in** est celui de l'integration event : il implémente
`ISignalsClients` (`SharedKernel.Contracts`) et nomme sa ressource
(`ResourceKind`, `ResourceId`). Le publisher écrit la ressource dans la
ligne d'outbox (`resource_kind`, `resource_id` — DDL idempotent, ADR
0021) : le suiveur n'a aucun catalogue à tenir, il lit deux colonnes. Un
module n'a rien d'autre à faire ; un fait qui n'est pas publié ne peut pas
être signalé — signaler commence par publier.

Le socle n'autorise rien (ADR 0023) ; il laisse un endroit exact où
décider : le port `ISignalPolicy` (Acteur + Signal → oui/non), défaut
« tout passe », consulté au fan-out avec l'Acteur de la connexion. Même
geste qu'`ICurrentActor`.

## Options écartées

- **WebSocket / SignalR** : bidirectionnel, mais aucun cas où le client
  pousse quelque chose que le HTTP ne dit pas déjà ; un hub hors MVC —
  hors `ModuleController`, hors Description, hors `DomainExceptionFilter` —
  une seconde surface qui échappe à tout ce que le socle épingle.
- **Les domain events comme source** : latence nulle, mais le client
  apprendrait un fait qui peut être rollbacké, et « effet de bord externe
  = outbox » est la doctrine de l'ADR 0014.
- **Un handler d'integration event du socle « vers les clients »** : il
  tournerait dans la même passe que le handler du Ledger, avant ou après
  lui selon l'ordre d'enregistrement — le client pourrait être prévenu
  avant que la ligne comptable existe, et le double-reload reviendrait.
  Après le marquage « livrée », toutes les conséquences in-process du fait
  sont en base. Corollaires assumés : un handler qui échoue retarde le
  Signal jusqu'au retry qui réussit ; une ligne poison ne signale jamais.
- **`LISTEN`/`NOTIFY` Postgres** : latence nulle, mais un hosted service
  de plus, une connexion longue par base de module et une reconnexion à
  écrire ; le marquage « livrée » suffit comme notification.
- **Limitation « instance unique » documentée** : contredit l'ADR 0021.
- **Un payload porté** : une seconde représentation qui diverge du `GET`
  sans que rien ne le signale — ce que l'ADR 0011 refuse aux commandes.
- **Un flux par module ou par ressource** : plus REST, mais un `EventSource`
  par ressource affichée (six par origine en HTTP/1.1) ; le filtre en
  paramètre garde une connexion par onglet et laisse le socle porter le
  seul point de montage.
- **Rejeu sur `Last-Event-ID`** : une seconde source de vérité pour un
  message dont le seul sens est « refais ton `GET` ».
- **`DropOldest` sur un client lent** : un rafraîchissement manqué sans
  rien pour le rattraper. Le canal borné se ferme au débordement,
  `EventSource` reconnecte, le front relit — même logique que le poison :
  on ne cache pas un débit qu'on ne tient pas.
- **AsyncAPI** : un second document et un second générateur pour un
  schéma de quatre champs ; la Description sait décrire un `GET` en
  `text/event-stream`.
- **Une trace par Signal** : bruit sans valeur, le fait est déjà tracé par
  sa livraison (ADR 0025). Une jauge des abonnés et un compteur des
  livraisons suffisent.

## Conséquences

- Deux colonnes nullables sur `__outbox`, `resource_kind` et `resource_id`,
  posées par le DDL idempotent ; nulles pour un event qui ne signale pas.
- Le suiveur démarre son curseur à l'Instant du démarrage : ce qui a été
  livré pendant qu'une instance était absente ne la regarde pas, ses
  clients n'y étaient pas connectés. Une fenêtre de recouvrement et une
  mémoire courte des ids absorbent le marquage « livrée » commité juste
  après une lecture.
- Un Signal peut arriver jusqu'à deux secondes après le commit de la
  commande : une passe pour livrer, une pour suivre.
- Le front relit toujours tout de suite après sa propre commande — le
  Signal ne remplace pas le CQS, il couvre ce que le client ne pouvait pas
  savoir.
- `INVALID_SIGNAL_RESOURCE` rejoint l'énumération des codes d'erreur : un
  filtre mal formé est une erreur métier du socle, comme un IBAN invalide.
- La Description gagne un media type de réponse de plus,
  `text/event-stream`, sur cette seule opération.
