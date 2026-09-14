# LoreBank

Base de départ pour un backend .NET en **monolithe modulaire, Clean
Architecture / DDD**.

Usage : cloner ce repo, le renommer, et construire ses modules métier sur le
socle fourni. Le module `Bank` sert d'exemple de référence, et le
`SharedKernel` apporte les blocs de base DDD (agrégats, value objects, domain
events).

## Structure

```
LoreBank/
├── backend/     # .NET — solution, SharedKernel, modules, Description OpenAPI
└── frontend/    # Vite + React + TypeScript — le Client généré depuis la Description
```

## Prérequis

Les versions d'outils sont gérées par [mise](https://mise.jdx.dev).

```bash
mise install
docker compose up -d          # PostgreSQL, Jaeger (traces), Prometheus (métriques)
cd backend && mise exec -- dotnet build LoreBank.slnx
mise run migrate              # l'API ne migre jamais au démarrage
mise exec -- dotnet run --project LoreBank.Host
```

Avant de pousser, `mise run check` à la racine rejoue toutes les portes de
qualité de la CI (build sans warning, format, Description OpenAPI à jour,
suite complète, front typé, linté, formaté, testé, construit, audité — `docs/qualite.md`) ;
`mise generate git-pre-commit --write` installe les rapides en hook.

Côté front, les types de l'API se génèrent depuis
`backend/openapi/lorebank.json`, émis à chaque build de l'hôte et commité :

```bash
cd frontend && mise exec -- npm ci   # génère src/api/schema.d.ts (prepare)
mise exec -- npm run dev
```

## Conventions

Les conventions d'architecture et de code sont décrites dans
[CLAUDE.md](CLAUDE.md), et des skills Claude Code accompagnent les gestes
récurrents dans `.claude/skills/`. [AGENTS.md](AGENTS.md) y renvoie, pour les
agents qui suivent cette convention plutôt que celle de Claude Code.

`.claude/settings.json` embarque trois hooks (ADR 0033) qui n'ajoutent
aucune règle : ils rappellent de lancer les portes, refusent l'édition à la
main d'un fichier généré, et posent la table chemin → skill. Ils ne
s'exécutent que sous Claude Code, et seulement après que vous ayez accordé
votre confiance à l'espace de travail. Travailler autrement ne fait rien
perdre des règles — elles sont toutes tenues par les portes — seulement le
rappel : `mise run hook:verify` et `mise run hook:generated` répondent la
même chose dans un terminal.

`.claude/agents/relecteur.md` est le relecteur à contexte frais (ADR 0038) :
un sous-agent qui relit un diff avec l'issue, `docs/qualite.md`, le
glossaire et les ADR touchés, et rend les écarts qu'aucun test ne tient.
Même statut que les hooks : sans Claude Code, le fichier se donne tel quel,
avec le diff, à ce que vous utilisez pour relire.

## Documentation

- [Les erreurs, de bout en bout](docs/erreurs.md) — comment une règle métier
  violée devient un JSON que le front sait traduire, et ce que renvoie l'API
  dans tous les autres cas.
- [La Description OpenAPI, de l'action au type](docs/openapi.md) — comment
  la surface HTTP est décrite depuis le code, commitée, et devient le Client
  TypeScript du front.
- [Les portes de qualité, du poste à la CI](docs/qualite.md) — ce que la CI
  refuse et le geste mise qui le rejoue sur le poste, à l'identique.
- [Les ADR, la carte](docs/adr/README.md) — chaque décision d'architecture,
  par numéro et par thème, avec son statut ; et le gabarit du prochain.
