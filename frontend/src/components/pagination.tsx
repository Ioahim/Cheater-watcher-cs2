"use client";

import { useState } from "react";

const pages = ["1", "2", "3", "…", "N"];

export function Pagination() {
  const [current, setCurrent] = useState("1");

  return (
    <nav className="flex items-center justify-end gap-2 border-t border-border px-5 py-3">
      {pages.map((page) => {
        const active = page === current;
        const disabled = page === "…";
        return (
          <button
            key={page}
            type="button"
            disabled={disabled}
            onClick={() => setCurrent(page)}
            className={`size-9 rounded-lg border text-sm transition-colors ${
              active
                ? "border-primary bg-primary/15 font-semibold text-primary-light"
                : disabled
                  ? "cursor-default border-transparent text-faint"
                  : "border-border text-muted hover:bg-hover hover:text-foreground"
            }`}
          >
            {page}
          </button>
        );
      })}
    </nav>
  );
}
