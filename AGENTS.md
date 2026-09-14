# AGENTS.md

Consignes pour tout agent qui travaille dans ce repo.

Ce fichier ne porte aucune règle : il renvoie, pour qu'un agent qui suit la
convention `AGENTS.md` trouve la même doctrine qu'un agent qui lit
`CLAUDE.md`. Deux fichiers qui redisent la même chose divergent — celui-ci
reste court par construction.

- **[CLAUDE.md](CLAUDE.md)** — la doctrine : build et outillage,
  architecture, conventions du domaine, couches Application et
  Infrastructure, tests, style, vérification. **Commencez par là.**
- **[`.claude/skills/`](.claude/skills/)** — les procédures de construction,
  une par geste récurrent (value object, agrégat, handler de domain event,
  commande, query, migration de schéma, migration de données, module), plus
  le chapeau qui déroule une issue de bout en bout. Ce sont des fichiers
  Markdown : ils se lisent sans Claude Code, et chacun porte sa table des
  garde-fous — la règle, et le test qui rougit si elle casse.
- **[`.claude/agents/`](.claude/agents/)** — le relecteur à contexte frais
  (ADR 0038) : un Markdown à donner tel quel, avec le diff, à ce que vous
  utilisez pour relire.
- **[CONTEXT.md](CONTEXT.md)** et **[`docs/adr/`](docs/adr/)** — le
  vocabulaire du socle, et les décisions qui l'ont façonné.
- **[`docs/agents/`](docs/agents/)** — les conventions de travail : issue
  tracker, labels de triage, domain docs.
