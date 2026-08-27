"use client";

import type { Account } from "@/lib/types";
import { CompetitiveBadge, PremierBadge, WingmanBadge } from "./rank-badge";

interface AccountCardProps {
  account: Account;
  selected: boolean;
  onSelect: () => void;
}

export function AccountCard({ account, selected, onSelect }: AccountCardProps) {
  const topComp = [...account.competitiveRanks].sort(
    (a, b) => b.level - a.level,
  )[0];

  return (
    <button
      type="button"
      onClick={onSelect}
      aria-pressed={selected}
      className={`flex w-40 flex-col items-center gap-3 rounded-xl px-4 py-5 transition-colors ${
        selected
          ? "border-l-2 border-primary bg-card"
          : "border-l-2 border-transparent hover:bg-hover"
      }`}
    >
      <span
        className={`flex size-12 items-center justify-center rounded-full text-sm font-bold transition-colors ${
          selected ? "bg-primary text-white" : "bg-surface text-primary-light"
        }`}
      >
        {account.name.charAt(0).toUpperCase()}
      </span>
      <span
        className={`text-sm font-medium ${selected ? "text-foreground" : "text-muted"}`}
      >
        {account.name}
      </span>
      <span className="flex flex-col items-center gap-1">
        {account.premierRating != null && (
          <PremierBadge rating={account.premierRating} />
        )}
        {account.wingmanLevel != null && (
          <WingmanBadge level={account.wingmanLevel} />
        )}
        {topComp && <CompetitiveBadge level={topComp.level} />}
      </span>
    </button>
  );
}
