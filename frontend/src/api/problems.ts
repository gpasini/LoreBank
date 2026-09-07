import type { ApiProblem } from "./client";
import { errorMessages } from "./errorMessages";

// Le passage d'un ApiProblem à ce que l'utilisateur lit. Le 500 est la seule
// réponse sans `code` : un message générique par statut. Un 400 de binding
// porte en plus `parameters.fields`, les champs fautifs — à surligner, jamais
// à afficher tels quels.
export function translate(problem: ApiProblem): string {
  if (problem.code === undefined) {
    return `Erreur ${problem.status} — réessayez plus tard.`;
  }

  return errorMessages[problem.code](problem.parameters ?? {});
}

export function faultyFields(problem: ApiProblem): ReadonlySet<string> {
  const fields = problem.parameters?.fields;

  return new Set(Array.isArray(fields) ? fields.map(String) : []);
}
