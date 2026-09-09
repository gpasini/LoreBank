import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

// L'origine de l'API, la même pour le Client et pour le flux de Signaux :
// vide en dev (Vite relaie /api en même origine).
export const baseUrl = import.meta.env.VITE_API_URL ?? "";

// Le seul point d'entrée vers l'API : typé par le Client généré depuis la
// Description (schema.d.ts). Chaque appel rend `{ data, error }` — `data`
// typé par la réponse nominale de l'opération, `error` par ses réponses
// d'erreur, c'est-à-dire ApiProblem : `error.code` est un ErrorCode.
export const api = createClient<paths>({ baseUrl });

export type ApiProblem = components["schemas"]["ApiProblem"];

export type ErrorCode = components["schemas"]["ErrorCode"];
