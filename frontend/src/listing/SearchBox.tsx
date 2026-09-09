import { useEffect, useState } from "react";

// Le champ de recherche d'une Liste : la valeur tapée part après un court
// silence (debounce), pas à chaque touche — une requête par intention, pas
// par caractère.
export function SearchBox({
  value,
  onChange,
  placeholder,
  delay = 300,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  delay?: number;
}) {
  const [draft, setDraft] = useState(value);

  useEffect(() => {
    setDraft(value);
  }, [value]);

  useEffect(() => {
    if (draft === value) {
      return;
    }

    const timer = setTimeout(() => onChange(draft), delay);

    return () => clearTimeout(timer);
  }, [draft, value, delay, onChange]);

  return (
    <input
      type="search"
      className="search"
      value={draft}
      placeholder={placeholder ?? "Rechercher…"}
      aria-label={placeholder ?? "Rechercher"}
      onChange={(event) => setDraft(event.target.value)}
    />
  );
}
