"use client";

export function Pagination({
  page,
  pageCount,
  onPageChange,
}: {
  page: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}) {
  if (pageCount <= 1) return null;

  return (
    <nav
      aria-label="Pagination"
      className="flex items-center justify-end gap-2 border-t border-border px-5 py-3"
    >
      {pageItems(page, pageCount).map((item, index) => {
        if (item === "…") {
          return (
            <span key={`ellipsis-${index}`} className="px-1 text-faint">
              {item}
            </span>
          );
        }
        const active = item === page;
        return (
          <button
            key={item}
            type="button"
            aria-current={active ? "page" : undefined}
            onClick={() => onPageChange(item)}
            className={`size-9 rounded-lg border text-sm transition-colors ${
              active
                ? "border-primary bg-primary/15 font-semibold text-primary-light"
                : "border-border text-muted hover:bg-hover hover:text-foreground"
            }`}
          >
            {item}
          </button>
        );
      })}
    </nav>
  );
}

function pageItems(current: number, total: number): (number | "…")[] {
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }
  const windowStart = Math.max(2, current - 1);
  const windowEnd = Math.min(total - 1, current + 1);
  const items: (number | "…")[] = [1];
  if (windowStart > 2) items.push("…");
  for (let i = windowStart; i <= windowEnd; i++) items.push(i);
  if (windowEnd < total - 1) items.push("…");
  items.push(total);
  return items;
}