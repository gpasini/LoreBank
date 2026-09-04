---
name: ajouter-fonctionnalite
description: À utiliser quand une fonctionnalité est à implémenter depuis une issue du tracker local — « implémente l'issue NN », « déroule .scratch/<feature> » — ou quand un développement qui traverse plusieurs couches démarre sans plan. Orchestre les skills briques dans l'ordre et s'arrête à la vérification.
---

# Ajouter une fonctionnalité

## Principe

Le chapeau mange une issue bien formée et déroule les briques dans l'ordre des
dépendances. Il ne code rien que les skills briques ne décrivent : son travail
est le séquencement, la traçabilité dans l'issue, et la vérification finale.
Il s'arrête là — pas de commit : la revue humaine décide de la suite.

## Recette

1. **Prendre l'issue** : le ticket vit dans `.scratch/<feature>/issues/NN-<slug>.md`
   (convention `docs/agents/issue-tracker.md`), avec `spec.md` à côté. Seule
   une issue `Status: ready-for-agent` se déroule. La passer à
   `Status: claimed` avant tout travail.
2. **Vérifier le terrain** : lire `CONTEXT.md` (employer le vocabulaire du
   glossaire, pas ses synonymes) et les ADR que le chantier touche
   (`docs/adr/`). Ce que l'issue affirme sur le code existant se vérifie dans
   le code — une contradiction avec un ADR se signale, elle ne s'écrase pas.
3. **Exiger, pas deviner** : si l'issue laisse une décision structurante
   ouverte (quel module, quelle transition, quelle forme de lecture), la
   passer à `Status: needs-info` avec les questions sous `## Comments`, et
   s'arrêter. Une hypothèse silencieuse coûte plus cher qu'un aller-retour.
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
   terminer » : les honorer brique par brique, sans reporter les tests à la
   fin.
5. **Vérification finale** : build sans warning et **suite complète** verte
   (`mise exec -- dotnet test LoreBank.slnx`) — les tests d'architecture du
   socle (`ModuleCompositionTest`, `DomainConventionTest`,
   `ApplicationConventionTest`, contrats HTTP) sont le filet de tout ce qu'une
   consigne aurait pu manquer.
6. **Clore** : sous `## Comments` de l'issue, un récapitulatif — fichiers
   créés, use cases exposés, tests ajoutés, écarts éventuels avec la spec —
   puis `Status: ready-for-human`.

## Pièges

- Le périmètre est celui de l'issue : une amélioration adjacente repérée en
  route devient une **nouvelle issue** (`Status: needs-triage`), pas un
  élargissement silencieux du diff.
- Les briques se déroulent dans l'ordre du tableau : une commande écrite avant
  son agrégat force à inventer le domaine depuis le bord HTTP.
- L'issue est la mémoire du chantier : ce qui a été décidé en la déroulant
  s'écrit dans ses `## Comments`, pas seulement dans la conversation.

## Avant de terminer

L'étape 5 passée pour de vrai (la sortie des commandes fait foi), l'issue
close à l'étape 6 — et rien de commité : le diff reste en working tree pour la
revue.
