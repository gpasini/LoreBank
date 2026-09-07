# LoreBank — front

Vite + React + TypeScript. Node vient de mise (`mise install` à la racine).

Les types de l'API (`src/api/schema.d.ts`) sont le **Client** généré depuis
la Description OpenAPI commitée dans `backend/openapi/lorebank.json` :
`npm run generate` les régénère, `npm ci`/`npm install` le fait tout seul
(`prepare`). Ne jamais éditer ce fichier — il est ignoré par git et se
régénère. `npm run typecheck` est la garantie : un code d'erreur que le back
ajoute et que `src/api/errorMessages.ts` ne traduit pas ne compile pas.

Voir `docs/openapi.md` pour le chemin complet, de l'action au type.

```bash
cd frontend
mise exec -- npm ci
cp .env.example .env
mise exec -- npm run dev
```
