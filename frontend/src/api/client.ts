import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

// Le seul point d'entrée vers l'API : typé par le Client généré depuis la
// Description (schema.d.ts). Chaque appel rend `{ data, error }` — `data`
// typé par la réponse nominale de l'opération, `error` par ses réponses
// d'erreur, c'est-à-dire ApiProblem : `error.code` est un ErrorCode.
export const api = createClient<paths>({
  baseUrl: import.meta.env.VITE_API_URL ?? "",
});

export type ApiProblem = components["schemas"]["ApiProblem"];

export type ErrorCode = components["schemas"]["ErrorCode"];
