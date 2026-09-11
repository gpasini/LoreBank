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
    },
  };
});
