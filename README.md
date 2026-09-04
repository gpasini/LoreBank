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
├── backend/     # .NET — solution, SharedKernel, modules
└── frontend/    # à venir
```

## Prérequis

Les versions d'outils sont gérées par [mise](https://mise.jdx.dev).

```bash
mise install
docker compose up -d          # PostgreSQL
cd backend && mise exec -- dotnet build LoreBank.slnx
mise run migrate              # l'API ne migre jamais au démarrage
mise exec -- dotnet run --project LoreBank.Host
```

## Conventions

Les conventions d'architecture et de code sont décrites dans
[CLAUDE.md](CLAUDE.md), et des skills Claude Code accompagnent les gestes
récurrents dans `.claude/skills/`.

## Documentation

- [Les erreurs, de bout en bout](docs/erreurs.md) — comment une règle métier
  violée devient un JSON que le front sait traduire, et ce que renvoie l'API
  dans tous les autres cas.
