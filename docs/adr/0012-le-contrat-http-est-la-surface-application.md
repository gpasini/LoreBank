# Le contrat HTTP d'un module est sa surface Application

> Statut : accepté — 2026-09-04.

Le dossier `Contracts/` de l'Api recopiait champ pour champ les types de
l'Application : `OpenBankAccountRequest` était `OpenBankAccountCommand`,
`AmountRequest` était `DepositMoneyCommand` moins l'id de la route, et
`BankAccountResponse` recopiait `BankAccountResult` derrière un `From()` sans
contenu. Trois fichiers miroirs à recopier par module, qui ne renommaient
rien, ne masquaient rien, ne versionnaient rien. On supprime : le body se lie
directement sur la commande, et une lecture sert le `Results/` de sa query
tel quel — il est déjà taillé pour l'affichage, en primitives, sans modèle
d'écriture derrière. Sur une route mixte (`POST {id}/deposits`), la route est
autoritaire : le controller réécrit `command with { AccountId = id }`, et un
`accountId` posté dans le body est écrasé — la sémantique d'avant, où le
champ n'existait simplement pas.

Le couplage est assumé comme doctrine : dans ce monolithe où le front et le
back versionnent ensemble, renommer une propriété de commande ou de Result
est un breaking change HTTP que le compilateur ne signale pas — c'est
`CqsContractTest` qui épingle l'ensemble exact des clés JSON du `GET`. Et le
jour où une forme wire diverge du Result (champ calculé, forme allégée pour
une liste) : une autre query avec son propre Result, portée par
l'Application — `Contracts/` ne revient jamais. Qui veut découpler versionne
dans l'Application, pas dans l'Api.

## Options écartées

- **Garder `Contracts/`** : le découplage qu'il promet ne sert que si les
  formes divergent — et la doctrine du socle est précisément qu'elles ne
  divergent pas (le Result est la forme d'affichage par définition).
- **L'asymétrie réponses-seules** (servir le Result, garder les requests) :
  deux règles à cloner là où une suffit, pour un nœud route+body qui se
  règle en un `with` d'une ligne.
- **Un model binder custom** composant route + body : de la plomberie dans
  le socle pour économiser ce même `with` explicite.
- **L'id dans le body seulement** : casse le routage ressource REST.
