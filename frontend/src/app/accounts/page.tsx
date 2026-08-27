"use client";

import { useState } from "react";
import { AccountCard } from "@/components/account-card";
import { AuthCodeModal } from "@/components/auth-code-modal";
import { MatchCodeModal } from "@/components/match-code-modal";
import { Navbar } from "@/components/navbar";
import {
  CompetitiveBadge,
  PremierBadge,
  WingmanBadge,
} from "@/components/rank-badge";
import { mockAccounts } from "@/lib/mock-data";

export default function AccountsPage() {
  const [selectedId, setSelectedId] = useState(mockAccounts[0].id);
  const [authOpen, setAuthOpen] = useState(false);
  const [matchOpen, setMatchOpen] = useState(false);

  const selected =
    mockAccounts.find((a) => a.id === selectedId) ?? mockAccounts[0];

  const settingsActions = [
    {
      label: "Reorder accounts",
      onClick: () => {},
    },
    {
      label: "Auth codes",
      onClick: () => setAuthOpen(true),
    },
    {
      label: "Add match manually",
      onClick: () => setMatchOpen(true),
    },
    {
      label: "Remove account",
      onClick: () => {},
      danger: true,
    },
  ];

  return (
    <>
      <Navbar />
      <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col px-4 py-8">
        <div className="my-auto flex flex-col gap-8">
          <section className="space-y-4">
            <h1 className="text-center text-xl font-semibold">Accounts</h1>
            <div className="grid grid-cols-2 justify-items-center gap-4 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
              {mockAccounts.map((account) => (
                <AccountCard
                  key={account.id}
                  account={account}
                  selected={account.id === selectedId}
                  onSelect={() => setSelectedId(account.id)}
                />
              ))}
            </div>
          </section>

          <section className="space-y-4">
            <div className="flex items-baseline justify-between gap-4">
              <h2 className="text-xl font-semibold">{selected.name} ranks</h2>
              <span className="text-xs text-faint">
                Wingman and Premier ranks are shared across all maps
              </span>
            </div>
            <div className="mx-auto w-full max-w-4xl space-y-3">
              {selected.premierRating != null && (
                <RankRow label="Premier" hint="CS Rating — all modes pool">
                  <PremierBadge rating={selected.premierRating} />
                </RankRow>
              )}
              {selected.wingmanLevel != null && (
                <RankRow label="Wingman" hint="Same skill group on every map">
                  <WingmanBadge level={selected.wingmanLevel} />
                </RankRow>
              )}
              {selected.competitiveRanks.length > 0 && (
                <div className="space-y-3 rounded-xl bg-card p-4">
                  <p className="text-sm font-medium text-muted">
                    Competitive{" "}
                    <span className="text-xs font-normal text-faint">
                      one rank per map
                    </span>
                  </p>
                  <div className="flex flex-wrap gap-x-4 gap-y-2">
                    {[...selected.competitiveRanks]
                      .sort((a, b) => b.level - a.level)
                      .map((r) => (
                        <span key={r.map} className="flex items-center gap-2">
                          <CompetitiveBadge level={r.level} />
                          <span className="text-xs text-muted">{r.map}</span>
                        </span>
                      ))}
                  </div>
                </div>
              )}
            </div>
          </section>

          <section className="space-y-4">
            <h2 className="text-center text-xl font-semibold">
              Account settings
            </h2>
            <div className="mx-auto grid w-full max-w-4xl gap-2 sm:grid-cols-2 lg:grid-cols-4">
              {settingsActions.map((action) => (
                <button
                  key={action.label}
                  type="button"
                  onClick={action.onClick}
                  className={`rounded-lg px-4 py-3 text-sm font-medium transition-colors ${
                    action.danger
                      ? "text-danger hover:bg-danger/10"
                      : "text-muted hover:bg-hover hover:text-foreground"
                  }`}
                >
                  {action.label}
                </button>
              ))}
            </div>
          </section>
        </div>
      </main>

      <AuthCodeModal open={authOpen} onClose={() => setAuthOpen(false)} />
      <MatchCodeModal open={matchOpen} onClose={() => setMatchOpen(false)} />
    </>
  );
}

function RankRow({
  label,
  hint,
  children,
}: {
  label: string;
  hint: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between rounded-xl bg-card px-4 py-3">
      <div>
        <p className="text-sm font-medium">{label}</p>
        <p className="text-xs text-faint">{hint}</p>
      </div>
      {children}
    </div>
  );
}
