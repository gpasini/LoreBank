import type { ApiProblem } from "../api/client";
import { translate } from "../api/problems";

export function Problem({ problem }: { problem: ApiProblem | null }) {
  if (!problem) {
    return null;
  }

  return (
    <p className="problem" role="alert">
      {translate(problem)}
    </p>
  );
}
