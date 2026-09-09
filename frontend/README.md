# LoreBank — front

Vite + React + TypeScript. Node vient de mise (`mise install` à la racine).

Les types de l'API (`src/api/schema.d.ts`) sont le **Client** généré depuis
la Description OpenAPI commitée dans `backend/openapi/lorebank.json` :
`npm run generate` les régénère, `npm ci`/`npm install` le fait tout seul
(`prepare`). Ne jamais éditer ce fichier — il est ignoré par git et se
régénère. `npm run typecheck` est la garantie : un code d'erreur que le back
ajoute et que `src/api/errorMessages.ts` ne traduit pas ne compile pas.
`npm run check` (Biome : lint et format — `npm run format` corrige) et
`npm run audit` (vulnérabilités `high`, dev incluses) sont les deux autres
portes ; la CI les rejoue telles quelles (`docs/qualite.md`).

Voir `docs/openapi.md` pour le chemin complet, de l'action au type.

```bash
cd frontend
mise exec -- npm ci
cp .env.example .env          # /api relayé vers l'hôte .NET (proxy Vite)
mise exec -- npm run dev
```

En dev, le front appelle l'API en même origine et Vite relaie `/api` vers
l'hôte (`VITE_API_PROXY`) : le back n'ouvre pas de CORS. Servir le front sous
la même origine que l'API en production est une décision de déploiement.
