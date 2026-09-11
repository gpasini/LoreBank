# LoreBank — front

Vite + React + TypeScript. Node vient de mise (`mise install` à la racine).
Le front est un **modèle** (ADR 0036) : le socle se garde, l'exemple se
remplace — `docs/front.md` dit lequel est lequel, et comment on écrit un
écran (skill `nouvel-ecran`).

Les types de l'API (`src/api/schema.d.ts`) sont le **Client** généré depuis
la Description OpenAPI commitée dans `backend/openapi/lorebank.json` :
`npm run generate` les régénère, `npm ci`/`npm install` le fait tout seul
(`prepare`). Ne jamais éditer ce fichier — il est ignoré par git et se
régénère. `npm run typecheck` est la garantie : un code d'erreur que le back
ajoute et que `src/api/errorMessages.ts` ne traduit pas ne compile pas.

Les portes, dans l'ordre de la CI (`docs/qualite.md`) : `npm run typecheck`,
`npm run check` (Biome : lint et format — `npm run format` corrige),
`npm run test` (vitest sur jsdom : socle, écrans, conventions et Gel du
front), `npm run build` (`vite build` seul), `npm run audit`
(vulnérabilités `high`, dev incluses). `mise run check` à la racine les
rejoue toutes, back compris ; `npx vitest` pour la boucle de test.

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

Ce qu'un cloneur garde : `src/api/`, `src/listing/`, `src/signals/`,
`src/format.ts`, `src/test/`. Ce qu'il remplace : `src/components/`,
`src/App.tsx`, `src/styles.css`, et le contenu de `errorMessages.ts` — le
typecheck lui dit quels codes traduire.
