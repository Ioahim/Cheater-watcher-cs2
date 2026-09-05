"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Navbar } from "@/components/navbar";
import { PlayerDetailModal } from "@/components/player-detail-modal";
import { getAccountStats, getAccountsSummary } from "@/lib/api";
import { useAccounts } from "@/lib/use-accounts";
import { FLAG_REASONS, type AccountStats } from "@/lib/types";

export default function StatsPage() {
  const { accounts, activeAccountId, setActiveAccountId, loading: accountsLoading, error: accountsError } = useAccounts();
  const [stats, setStats] = useState<AccountStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [playerSteam64, setPlayerSteam64] = useState<string | null>(null);
  const [summaryMode, setSummaryMode] = useState(false);
  const fetchIdRef = useRef(0);

  const loadStats = useCallback(async (accountId: number | null) => {
    const id = ++fetchIdRef.current;
    setError(null);
    try {
      const data =
        accountId === null ? await getAccountsSummary() : await getAccountStats(accountId);
      if (fetchIdRef.current !== id) return;
      setStats(data);
    } catch {
      if (fetchIdRef.current !== id) return;
      setStats(null);
      setError("Could not load stats.");
    }
  }, []);

  useEffect(() => {
    const run = async () => {
      await Promise.resolve();
      if (accountsLoading) return;
      if (summaryMode) {
        setLoading(true);
        await loadStats(null);
        setLoading(false);
        return;
      }
      if (!activeAccountId) {
        setStats(null);
        setLoading(false);
        return;
      }
      setLoading(true);
      await loadStats(activeAccountId);
      setLoading(false);
    };
    void run();
  }, [accountsLoading, activeAccountId, summaryMode, loadStats]);

  const handleAccountSwitch = (accountId: number) => {
    setSummaryMode(false);
    setActiveAccountId(accountId);
  };

  return (
    <>
      <Navbar />
      <main className="mx-auto w-full max-w-6xl flex-1 space-y-6 px-4 py-8">
        <div className="flex flex-wrap items-center gap-2">
          {accounts.length > 0 && (
            <button
              type="button"
              onClick={() => setSummaryMode(true)}
              className={`rounded-lg px-4 py-2 text-sm transition-colors ${
                summaryMode
                  ? "bg-primary/10 font-medium text-primary-light"
                  : "text-muted hover:bg-hover hover:text-foreground"
              }`}
            >
              All accounts
            </button>
          )}
          {accounts.map((account) => (
            <button
              key={account.id}
              type="button"
              onClick={() => handleAccountSwitch(account.id)}
              className={`rounded-lg px-4 py-2 text-sm transition-colors ${
                !summaryMode && account.id === activeAccountId
                  ? "bg-primary/10 font-medium text-primary-light"
                  : "text-muted hover:bg-hover hover:text-foreground"
              }`}
            >
              {account.name}
            </button>
          ))}
        </div>

        {loading ? (
          <p className="py-8 text-center text-sm text-faint">Loading stats…</p>
        ) : (error || accountsError) ? (
          <p className="py-8 text-center text-sm text-danger">{error || accountsError}</p>
        ) : !stats ? (
          <p className="py-8 text-center text-sm text-faint">
            No data available - upload a .dem to start tracking.
          </p>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <StatCard label="Total matches" value={stats.totalMatches} />
              <StatCard label="Flagged matches" value={stats.flaggedMatches} accent="danger" />
              <StatCard label="Flagged players" value={stats.flaggedPlayers} accent="danger" />
              <StatCard
                label="Win rate against cheaters"
                value={`${Math.round(stats.winRate * 100)}%`}
                accent="success"
              />
            </div>

            {stats.byMode.length > 0 && (
              <section className="space-y-3">
                <h2 className="px-1 font-semibold">By mode</h2>
                <div className="flex flex-wrap gap-3">
                  {stats.byMode.map((m) => (
                    <span
                      key={m.mode}
                      className="rounded-lg bg-card px-4 py-3 text-sm"
                    >
                      <span className="font-medium">{m.mode}</span>{" "}
                      <span className="text-muted">({m.matches})</span>
                    </span>
                  ))}
                </div>
              </section>
            )}

            {stats.byMap.length > 0 && (
              <section className="space-y-3">
                <h2 className="px-1 font-semibold">By map</h2>
                <div className="space-y-2">
                  {stats.byMap.map((m) => (
                    <div
                      key={m.map}
                      className="flex items-center justify-between rounded-lg bg-card px-4 py-3 text-sm"
                    >
                      <span className="font-medium">{m.map}</span>
                      <span className="text-muted">
                        {m.matches} matches ·{" "}
                        <span className={m.winRate >= 0.5 ? "text-success" : "text-danger"}>
                          {Math.round(m.winRate * 100)}%
                        </span>{" "}
                        win rate
                      </span>
                    </div>
                  ))}
                </div>
              </section>
            )}

            <section className="space-y-3">
              <h2 className="px-1 font-semibold">Players</h2>
              <p className="px-1 text-sm text-muted">
                Total unique players encountered: {stats.totalPlayers}
              </p>
            </section>

            {stats.flaggedPlayersList.length > 0 && (
              <section className="space-y-3">
                <h2 className="px-1 font-semibold">Flagged players</h2>
                <div className="space-y-2">
                  {stats.flaggedPlayersList.map((p) => {
                    const reason = FLAG_REASONS.find((r) => r.value === p.flagReason) ?? FLAG_REASONS[0];
                    return (
                      <button
                        key={p.steam64Id}
                        type="button"
                        onClick={() => setPlayerSteam64(p.steam64Id)}
                        className="flex w-full items-center gap-2 rounded-lg bg-card px-4 py-3 text-left text-sm transition-colors hover:bg-hover"
                      >
                        <span className="font-medium">{p.name}</span>
                        <span aria-hidden="true"> - </span>
                        <span className={`${reason.color} text-xs`}>{reason.label}</span>
                        {p.vacBanned && <span className="text-xs text-danger">VAC banned</span>}
                        <span className="ml-auto text-xs text-muted">
                          {p.encounters} encounter(s)
                          {p.flagNote ? ` · ${p.flagNote}` : ""}
                        </span>
                      </button>
                    );
                  })}
                </div>
              </section>
            )}
          </>
        )}
      </main>
      {playerSteam64 && (
        <PlayerDetailModal steam64Id={playerSteam64} open onClose={() => setPlayerSteam64(null)} />
      )}
    </>
  );
}

function StatCard({
  label,
  value,
  accent,
}: {
  label: string;
  value: number | string;
  accent?: "danger" | "success";
}) {
  const valueColor =
    accent === "danger"
      ? "text-danger"
      : accent === "success"
        ? "text-success"
        : "text-foreground";

  return (
    <div className="rounded-xl bg-card px-4 py-5 text-center">
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-1 text-xl font-bold ${valueColor}`}>{value}</p>
    </div>
  );
}
