import {
  createContext,
  type ReactNode,
  useCallback,
  useContext,
  useEffect,
  useRef,
} from "react";
import { baseUrl } from "../api/client";
import type { components, paths } from "../api/schema";

// Le Signal (ADR 0026) : le socle prévient qu'un fait a été livré — quoi et
// où, jamais comment. Un seul EventSource par onglet, ouvert ici, non
// filtré : chaque composant s'abonne à la ressource qu'il affiche et relit
// son GET. Le chemin vient de la Description : s'il bouge, le typecheck
// rougit. Le navigateur reconnecte seul ; après une coupure, tout le monde
// relit — un Signal manqué n'est jamais rattrapé autrement.
export type Signal = components["schemas"]["Signal"];

const signalsPath = "/api/signals" satisfies keyof paths;

type Subscription = {
  kind: string;
  id: string | null;
  refresh: () => void;
};

const SignalsContext = createContext<{
  subscribe: (subscription: Subscription) => () => void;
} | null>(null);

export function SignalsProvider({ children }: { children: ReactNode }) {
  const subscriptions = useRef(new Set<Subscription>());

  useEffect(() => {
    const source = new EventSource(`${baseUrl}${signalsPath}`);
    let dropped = false;

    source.onmessage = (event: MessageEvent<string>) => {
      const signal = JSON.parse(event.data) as Signal;

      for (const subscription of subscriptions.current) {
        if (
          subscription.kind === signal.resourceKind &&
          (subscription.id === null || subscription.id === signal.resourceId)
        ) {
          subscription.refresh();
        }
      }
    };

    source.onerror = () => {
      dropped = true;
    };

    source.onopen = () => {
      if (!dropped) {
        return;
      }

      dropped = false;

      for (const subscription of subscriptions.current) {
        subscription.refresh();
      }
    };

    return () => source.close();
  }, []);

  const subscribe = useCallback((subscription: Subscription) => {
    subscriptions.current.add(subscription);

    return () => {
      subscriptions.current.delete(subscription);
    };
  }, []);

  return (
    <SignalsContext.Provider value={{ subscribe }}>
      {children}
    </SignalsContext.Provider>
  );
}

// S'abonner aux Signaux d'un genre de ressource — d'une instance, ou de
// toutes (`id` nul) — et relire à chacun. `refresh` est relu à chaque rendu :
// l'abonnement, lui, ne bouge que si la ressource change.
export function useSignals(
  kind: string,
  id: string | null,
  refresh: () => void,
) {
  const context = useContext(SignalsContext);
  const latest = useRef(refresh);

  latest.current = refresh;

  useEffect(
    () => context?.subscribe({ kind, id, refresh: () => latest.current() }),
    [context, kind, id],
  );
}
