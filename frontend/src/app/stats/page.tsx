"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Navbar } from "@/components/navbar";
import { useAuth } from "@/components/auth-provider";
import { getAccountStats, getAccounts } from "@/lib/api";
import { mockAccounts } from "@/lib/mock-data";
import type { Account, AccountStats } from "@/lib/types";

export default function StatsPage() {
  const { user, loading: authLoading } = useAuth();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [activeAccountId, setActiveAccountId] = useState<number | null>(null);
  const [stats, setStats] = useState<AccountStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const fetchIdRef = useRef(0);

  const loadStats = useCallback(async (accountId: number) => {
    const id = ++fetchIdRef.current;
    setError(null);
    try {
      const data = await getAccountStats(accountId);
      if (fetchIdRef.current !== id) return;
      setStats(data);
    } catch {
      if (fetchIdRef.current !== id) return;
      setStats(null);
      setError("Could not load stats.");
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (authLoading) return;
      if (!user) {
        setAccounts(mockAccounts);
        setActiveAccountId(mockAccounts[0]?.id ?? null);
        setStats(null);
        setLoading(false);
        return;
      }
      setLoading(true);
      setError(null);
      try {
        const liveAccounts = await getAccounts();
        if (cancelled) return;
        setAccounts(liveAccounts);
        const firstId = liveAccounts[0]?.id;
        if (firstId) {
          setActiveAccountId(firstId);
          await loadStats(firstId);
        } else {
          setStats(null);
        }
      } catch {
        setStats(null);
        setError("Could not load stats.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [authLoading, user, loadStats]);

  const handleAccountSwitch = async (accountId: number) => {
    setActiveAccountId(accountId);
    setError(null);
    if (!user) {
      setStats(null);
      return;
    }
    setLoading(true);
    const id = ++fetchIdRef.current;
    try {
      const data = await getAccountStats(accountId);
      if (fetchIdRef.current === id) setStats(data);
    } catch {
      if (fetchIdRef.current === id) {
        setStats(null);
        setError("Could not load stats.");
      }
    } finally {
      if (fetchIdRef.current === id) setLoading(false);
    }
  };

  return (
    <>
      <Navbar />
      <main className="mx-auto w-full max-w-6xl flex-1 space-y-6 px-4 py-8">
        <div className="flex items-center gap-2">
          {accounts.map((account) => (
            <button
              key={account.id}
              type="button"
              onClick={() => handleAccountSwitch(account.id)}
              className={`rounded-lg px-4 py-2 text-sm transition-colors ${
                account.id === activeAccountId
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
        ) : error ? (
          <p className="py-8 text-center text-sm text-danger">{error}</p>
        ) : !stats ? (
          <p className="py-8 text-center text-sm text-faint">
            No data available - upload a .dem or connect a share code.
          </p>
        ) : (
          <>
            <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
              <StatCard label="Total matches" value={stats.totalMatches} />
              <StatCard label="Flagged matches" value={stats.flaggedMatches} accent="danger" />
              <StatCard label="Flagged players" value={stats.flaggedPlayers} accent="danger" />
              <StatCard
                label="Win rate"
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
              {stats.flaggedPlayers > 0 && (
                <p className="px-1 text-sm text-danger">
                  {stats.flaggedPlayers} player(s) flagged across all matches.
                </p>
              )}
            </section>
          </>
        )}
      </main>
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
    <div className="rounded-xl bg-card px-4 py-5">
      <p className="text-xs text-muted">{label}</p>
      <p className={`mt-1 text-xl font-bold ${valueColor}`}>{value}</p>
    </div>
  );
}
