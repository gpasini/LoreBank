/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { defineConfig, loadEnv } from "vite";

// En dev, le front appelle l'API en même origine : Vite relaie /api vers
// l'hôte .NET (VITE_API_PROXY). Le back n'ouvre pas de CORS — servir le front
// sous la même origine que l'API est une décision de déploiement, pas du socle.
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), "");

  return {
    plugins: [react()],
    server: {
      proxy: {
        "/api": env.VITE_API_PROXY ?? "http://localhost:5103",
      },
    },
    // Les tests tournent dans jsdom, colocalisés (*.test.ts, *.test.tsx) ;
    // les helpers vivent dans src/test/ (docs/front.md).
    test: {
      environment: "jsdom",
      include: ["src/**/*.test.{ts,tsx}"],
      setupFiles: ["src/test/setup.ts"],
      // Une origine absolue : le Client construit des Request, et Node
      // refuse une URL relative. Le stub de fetch (src/test/) ne lit que le
      // chemin.
      env: { VITE_API_URL: "http://api.test" },
      // La carte de couverture (ADR 0029, appliqué au front) : une mesure,
      // pas une porte — `mise run coverage`, hors de `check`, sans seuil.
      // Tout src/ est mesuré, y compris ce qu'aucun test ne touche ; sort
      // ce qu'aucun test ne vise. Les deux listes sont gelées (gel.test.ts).
      coverage: {
        provider: "v8",
        include: ["src/**/*.{ts,tsx}"],
        exclude: [
          "src/api/schema.d.ts",
          "src/vite-env.d.ts",
          "src/test/**",
          "src/**/*.test.{ts,tsx}",
          "src/main.tsx",
        ],
        // Le texte montre aussi les fichiers à 100 % : la carte est
        // entière dans la page du run, pas seulement ses trous.
        reporter: [["text", { skipFull: false }], "html"],
      },
    },
  };
});
