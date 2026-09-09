// Le pager d'une Liste (ADR 0027) : précédent, « page x sur n », suivant —
// n se calcule du totalCount que la Page porte toujours.
export function Pager({
  page,
  pageSize,
  totalCount,
  onChange,
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  onChange: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(totalCount / pageSize));

  if (pages === 1 && page === 1) {
    return null;
  }

  return (
    <nav className="pager" aria-label="Pagination">
      <button type="button" disabled={page <= 1} onClick={() => onChange(page - 1)}>
        Précédent
      </button>
      <span className="muted">
        page {page} sur {pages}
      </span>
      <button type="button" disabled={page >= pages} onClick={() => onChange(page + 1)}>
        Suivant
      </button>
    </nav>
  );
}
