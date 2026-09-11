---
name: ajouter-fonctionnalite
description: À utiliser quand une fonctionnalité est à implémenter depuis une issue GitHub du repo — « implémente l'issue #NN », « déroule le ticket NN » — ou quand un développement qui traverse plusieurs couches démarre sans plan. Orchestre les skills briques dans l'ordre et s'arrête à la vérification.
---

# Ajouter une fonctionnalité

## Principe

Le chapeau mange une issue bien formée et déroule les briques dans l'ordre des
dépendances. Il ne code rien que les skills briques ne décrivent : son travail
est le séquencement, la traçabilité dans l'issue, et la vérification finale.
Il s'arrête là — pas de commit : la revue humaine décide de la suite.

## Recette

1. **Prendre l'issue** : `gh issue view NN --comments` (convention
   `docs/agents/issue-tracker.md`), et l'issue de spec qu'elle cite en
   chapeau. Seule une issue portant le label `ready-for-agent` se déroule.
   La revendiquer avant tout travail : `gh issue edit NN --add-assignee @me`.
2. **Vérifier le terrain** : lire `CONTEXT.md` (employer le vocabulaire du
   glossaire, pas ses synonymes) et les ADR que le chantier touche
   (`docs/adr/`). Ce que l'issue affirme sur le code existant se vérifie dans
   le code — une contradiction avec un ADR se signale, elle ne s'écrase pas.
3. **Exiger, pas deviner** : si l'issue laisse une décision structurante
   ouverte (quel module, quelle transition, quelle forme de lecture), poser
   les questions en commentaire (`gh issue comment NN`), basculer le label
   (`gh issue edit NN --add-label needs-info --remove-label ready-for-agent`)
   et s'arrêter. Une hypothèse silencieuse coûte plus cher qu'un
   aller-retour.
4. **Dérouler les briques**, chacune par sa skill, dans l'ordre des
   dépendances :

   | Besoin | Skill |
   |---|---|
   | Aucun module existant ne convient | `nouveau-module` (en premier) |
   | Donnée typée nouvelle ou primitif nu | `nouveau-value-object` |
   | Concept à identité, ou transition d'état | `nouvel-agregat` |
   | Réaction à un fait métier | `nouveau-domain-event-handler` |
   | Le modèle persisté change | `nouvelle-migration-schema` |
   | Des lignes existantes à transformer | `nouvelle-data-migration` |
   | Opération d'écriture exposée en HTTP | `nouvelle-commande` |
   | Lecture exposée en HTTP | `nouvelle-query` |

   Chaque skill porte sa recette, ses garde-fous et son « Avant de
   terminer » : les honorer brique par brique. Leur **ordre** est celui de
   l'ADR 0032 — squelette, test, **RED observé**, corps — et il n'est pas
   négociable : un test écrit après le code est taillé sur lui, et aucune
   porte ne voit la différence. Noter le rouge de chaque brique au passage,
   il est exigé à l'étape 6.
5. **Vérification finale** : `mise run check` à la racine — toutes les
   portes de qualité dans l'ordre de la CI (ADR 0028, `docs/qualite.md`) :
   build sans warning, format, Description à jour, **suite complète** verte,
   front typé, linté, formaté, audité — les tests d'architecture du socle
   (`ModuleCompositionTest`, `DomainConventionTest`,
   `ApplicationConventionTest`, contrats HTTP) sont le filet de tout ce
   qu'une consigne aurait pu manquer. Un fichier hors format se corrige par
   `mise run format`, jamais à la main.
6. **Clore** : un commentaire de récapitulatif (`gh issue comment NN`) —
   fichiers créés, use cases exposés, tests ajoutés, écarts éventuels avec
   la spec — **plus une section « RED observés »** : un tableau brique /
   test / message d'échec, une ligne par brique (et une seule ligne s'il n'y
   en a qu'une). C'est la seule trace que laisse un RED, et elle ne
   s'invente pas après coup : elle se recopie du run rouge. Une brique
   exemptée (`nouvelle-migration-schema`) le dit dans sa ligne. Puis
   `gh issue edit NN --add-label ready-for-human
   --remove-label ready-for-agent`. Pas de `gh issue close` : c'est la revue
   humaine qui ferme, une fois le diff intégré.

## Pièges

- Le périmètre est celui de l'issue : une amélioration adjacente repérée en
  route devient une **nouvelle issue** (`gh issue create --label
  needs-triage`), pas un élargissement silencieux du diff.
- Les briques se déroulent dans l'ordre du tableau : une commande écrite avant
  son agrégat force à inventer le domaine depuis le bord HTTP.
- L'issue est la mémoire du chantier : ce qui a été décidé en la déroulant
  s'écrit dans ses commentaires GitHub, pas seulement dans la conversation.

## Avant de terminer

L'étape 5 passée pour de vrai (la sortie des commandes fait foi), un RED
observé par brique et recopié dans le récap, l'issue passée en
`ready-for-human` à l'étape 6 — et rien de commité : le diff reste en working
tree pour la revue.
