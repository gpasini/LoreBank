# Les ADR : la carte

Une décision par fichier, dans l'ordre où elle a été prise. Le numéro est un
identifiant : il est cité dans CLAUDE.md, les skills, le code, les csproj, et
il ne change pas. Un ADR dépassé ne se supprime pas et ne se renumérote pas :
il se marque **remplacé** dans sa ligne Statut, et l'ADR qui le remplace le
dit aussi. Les autres liens — *complété par*, *prolongé par*, *amende* —
laissent l'ADR en doctrine courante.

Cette carte est écrite à la main et tenue vraie par `AdrIndexTest`
(`backend/LoreBank.SharedKernel.Test.Infrastructure/Hosting/`) : une ligne
par fichier, le titre du `#`, le statut dérivé de la ligne `> Statut`, un
seul thème par ADR, et toute mention « ADR NNNN » du repo qui désigne un
fichier. Décision et alternatives : ADR 0037.

## Par numéro

| ADR | Titre | Statut | Thème |
|---|---|---|---|
| [0001](0001-montage-de-module-via-ihostmodule.md) | Montage de module via IHostModule | accepté | Montage de module |
| [0002](0002-socle-de-test-integration-partage.md) | Socle de test d'intégration partagé | accepté | Harnais de test |
| [0003](0003-dispatch-des-events-via-moduledbcontext.md) | Dispatch des events via ModuleDbContext | accepté | Inter-modules |
| [0004](0004-emprunt-de-connexion-via-modulereader.md) | Emprunt de connexion via ModuleReader | remplacé par 0018 | Persistance |
| [0005](0005-le-type-du-dbcontext-declare-sur-le-seam.md) | Le type du DbContext déclaré sur le seam | accepté | Montage de module |
| [0006](0006-migrations-hors-du-processus-api.md) | Migrations hors du processus API | accepté | Persistance |
| [0007](0007-l-identite-du-module-derivee-du-dbcontext.md) | L'identité du module dérivée du DbContext | accepté | Montage de module |
| [0008](0008-la-persistance-par-defaut-derivee-de-l-identite.md) | La persistance par défaut dérivée de l'identité | accepté | Montage de module |
| [0009](0009-le-schema-derive-de-l-identite.md) | Le schéma dérivé de l'identité | accepté | Montage de module |
| [0010](0010-le-repository-d-agregat-via-modulerepository.md) | Le repository d'agrégat via ModuleRepository | accepté | Persistance |
| [0011](0011-le-cqs-du-bord-http-porte-par-modulecontroller.md) | Le CQS du bord HTTP porté par ModuleController | accepté | Bord HTTP |
| [0012](0012-le-contrat-http-est-la-surface-application.md) | Le contrat HTTP d'un module est sa surface Application | accepté | Bord HTTP |
| [0013](0013-les-migrations-de-donnees-dans-la-timeline-du-schema.md) | Les migrations de données dans la timeline du schéma | accepté | Persistance |
| [0014](0014-outbox-in-process-pour-les-integration-events.md) | L'outbox in-process pour les integration events | accepté | Inter-modules |
| [0015](0015-les-contrats-comme-langage-publie-du-module.md) | Les Contrats comme langage publié du module | accepté | Inter-modules |
| [0016](0016-la-validation-aux-frontieres-la-confiance-a-la-rehydratation.md) | La validation aux frontières, la confiance à la réhydratation | accepté | Domaine |
| [0017](0017-le-socle-se-prouve-sur-son-propre-terrain.md) | Le socle se prouve sur son propre terrain — le ProbeModule | accepté | Harnais de test |
| [0018](0018-lectures-via-rows-keyless.md) | Lectures via rows keyless | accepté | Persistance |
| [0019](0019-la-description-openapi-derivee-et-commitee.md) | La Description OpenAPI dérivée de ModuleController et commitée | accepté | Bord HTTP |
| [0020](0020-la-version-d-agregat-portee-par-le-socle.md) | La Version d'agrégat portée par le socle | accepté | Persistance |
| [0021](0021-l-outbox-a-plusieurs-instances-reservation-et-retention.md) | L'outbox à plusieurs instances : réservation par bail et rétention | accepté | Inter-modules |
| [0022](0022-l-observabilite-du-socle-sans-dependance.md) | L'observabilité du socle sans dépendance | accepté | Observabilité |
| [0023](0023-l-authentification-hors-du-socle-l-acteur-dedans.md) | L'authentification hors du socle, l'Acteur dedans | accepté | Domaine |
| [0024](0024-le-temps-recu-jamais-demande.md) | Le temps reçu, jamais demandé | accepté | Domaine |
| [0025](0025-la-telemetrie-hors-du-socle-dans-l-hote.md) | La Télémétrie hors du socle, dans l'hôte | accepté | Observabilité |
| [0026](0026-le-signal-le-temps-reel-du-socle-nu-et-apres-livraison.md) | Le Signal : le temps réel du socle, nu et après livraison | accepté | Observabilité |
| [0027](0027-la-liste-forme-de-reference-du-socle.md) | La Liste : la forme de référence d'une lecture paginée, recherchée, facettée | accepté | Bord HTTP |
| [0028](0028-les-portes-de-qualite-outillees-pas-seulement-ecrites.md) | Les portes de qualité : outillées, pas seulement écrites | accepté | Portes et Gel |
| [0029](0029-la-couverture-une-mesure-pas-une-porte.md) | La couverture : une mesure, pas une porte | accepté | Portes et Gel |
| [0030](0030-le-dbsetup-en-scenario-differe.md) | Le DbSetup en scénario différé | accepté | Harnais de test |
| [0031](0031-le-gel-les-portes-se-gardent-elles-memes.md) | Le Gel : les portes se gardent elles-mêmes | accepté | Portes et Gel |
| [0032](0032-le-test-avant-le-code-le-red-observe-jamais-suppose.md) | Le test avant le code : le RED observé, jamais supposé | accepté | Cadrer l'agent |
| [0033](0033-le-hook-un-moment-pas-une-regle.md) | Le hook : un moment, pas une règle | accepté | Cadrer l'agent |
| [0034](0034-claude-md-un-routeur-pas-un-manuel.md) | CLAUDE.md : un routeur, pas un manuel | accepté | Cadrer l'agent |
| [0035](0035-une-regle-tenue-ou-assumee-jamais-esperee.md) | Une règle sans garde-fou est tenue ou assumée, jamais espérée | accepté | Cadrer l'agent |
| [0036](0036-le-front-est-un-modele-pas-une-vitrine.md) | Le front est un modèle, pas une vitrine | accepté | Front |
| [0037](0037-l-index-des-adr-une-carte-verifiee-pas-une-reorganisation.md) | L'index des ADR : une carte vérifiée, pas une réorganisation | accepté | Cadrer l'agent |
| [0038](0038-la-relecture-un-lecteur-frais-pas-l-auteur.md) | La relecture : un lecteur frais, pas l'auteur | accepté | Cadrer l'agent |
| [0039](0039-l-image-par-le-sdk-la-migration-par-l-orchestrateur.md) | L'image par le SDK, la migration par l'orchestrateur | accepté | Déploiement |

## Par thème

Un thème par ADR, celui de sa décision — pas de ses conséquences. Le
routage du geste en cours reste dans CLAUDE.md ; cette table dit tout ce
qui a été décidé sur un sujet.

| Thème | ADR |
|---|---|
| Montage de module | 0001, 0005, 0007, 0008, 0009 |
| Persistance | 0004, 0006, 0010, 0013, 0018, 0020 |
| Inter-modules | 0003, 0014, 0015, 0021 |
| Bord HTTP | 0011, 0012, 0019, 0027 |
| Domaine | 0016, 0023, 0024 |
| Harnais de test | 0002, 0017, 0030 |
| Observabilité | 0022, 0025, 0026 |
| Portes et Gel | 0028, 0029, 0031 |
| Cadrer l'agent | 0032, 0033, 0034, 0035, 0037, 0038 |
| Front | 0036 |
| Déploiement | 0039 |

## Écrire le prochain

Le numéro est le suivant du dernier de la table ; le fichier s'appelle
`NNNN-le-titre-en-kebab.md`. La ligne de l'ADR s'ajoute aux deux tables
ci-dessus, et l'ADR qu'il remplace, complète ou amende reçoit le lien
retour dans sa propre ligne Statut. La forme, que `AdrIndexTest` vérifie
sur tout ADR à partir du 0037 :

```markdown
# Le titre : la décision en une phrase

> Statut : accepté — AAAA-MM-JJ.

Le contexte : ce qui était en place, ce qui coinçait, et pourquoi
maintenant. Les faits vérifiés, pas les impressions.

## La décision

Ce qui change, et ce qui ne change pas. Les nouveaux mots, s'il y en a,
avec le nom qu'ils prennent dans CONTEXT.md.

## Garde-fous

| Règle | Test qui rougit |
|---|---|
| … | … |

S'il n'y en a aucun : le dire, et dire pourquoi la règle est assumée
plutôt que tenue (ADR 0035) — elle va alors dans docs/qualite.md.

## Options écartées

Chaque alternative sérieuse, avec la raison de l'écarter. C'est ce que le
prochain lecteur cherchera en premier.
```

Les sections « La mise en conformité » (ce qui a dû bouger dans le code
existant) et « Le coût assumé » restent libres.
