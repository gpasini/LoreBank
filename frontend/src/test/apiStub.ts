import type { Middleware } from "openapi-fetch";
import { api } from "../api/client";

// Le double de l'API pour les tests d'écran : un middleware sur le Client
// (openapi-fetch) qui répond à la place du réseau — des routes déclarées
// par méthode et chemin, une réponse JSON par route, et le journal des
// appels reçus. Rien à injecter dans le code testé : setup.ts l'éjecte
// après chaque test.
export type StubbedRoute = {
  method: "GET" | "POST" | "PUT" | "DELETE";
  path: string;
  status?: number;
  body?: unknown;
  headers?: Record<string, string>;
};

export type ReceivedCall = {
  method: string;
  path: string;
  body: unknown;
};

const installed = new Set<Middleware>();

export function stubApi(routes: StubbedRoute[]) {
  const calls: ReceivedCall[] = [];

  const middleware: Middleware = {
    async onRequest({ request }) {
      const path = new URL(request.url).pathname;
      const text = await request.text();

      calls.push({
        method: request.method,
        path,
        body: text === "" ? null : JSON.parse(text),
      });

      const route = routes.find(
        (candidate) =>
          candidate.method === request.method && candidate.path === path,
      );

      if (route === undefined) {
        throw new Error(`Aucune route stubée pour ${request.method} ${path}`);
      }

      const status = route.status ?? (route.body === undefined ? 204 : 200);

      return new Response(
        route.body === undefined ? null : JSON.stringify(route.body),
        {
          status,
          headers: {
            ...(route.body === undefined
              ? {}
              : { "content-type": "application/json" }),
            ...route.headers,
          },
        },
      );
    },
  };

  api.use(middleware);
  installed.add(middleware);

  return {
    calls,
    received: (method: string, path: string) =>
      calls.filter((call) => call.method === method && call.path === path),
  };
}

export function ejectStubs() {
  for (const middleware of installed) {
    api.eject(middleware);
  }

  installed.clear();
}
