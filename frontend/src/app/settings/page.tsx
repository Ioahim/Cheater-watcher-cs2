"use client";

import { useCallback, useEffect, useState } from "react";
import { AccountCard } from "@/components/account-card";
import { AuthCodesModal } from "@/components/auth-code-modal";
import { AddMatchModal } from "@/components/match-code-modal";
import { Navbar } from "@/components/navbar";
import {
  CompetitiveBadge,
  PremierBadge,
  UnrankedBadge,
  WingmanBadge,
} from "@/components/rank-badge";
import { useAuth } from "@/components/auth-provider";
import { getAccounts, getMe, getSteamLinkUrl, unlinkAccount } from "@/lib/api";
import type { Account } from "@/lib/types";

export default function SettingsPage() {
  const { user, loading: authLoading, setUser } = useAuth();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [linking, setLinking] = useState(false);
  const [authOpen, setAuthOpen] = useState(false);
  const [matchOpen, setMatchOpen] = useState(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError("");
    try {
      const live = await getAccounts();
      setAccounts(live);
      setSelectedId((prev) =>
        prev != null && live.some((a) => a.id === prev)
          ? prev
          : (live[0]?.id ?? null),
      );
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to load accounts.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      if (authLoading || !user) return;
      setLoading(true);
      setError("");
      try {
        const live = await getAccounts();
        if (cancelled) return;
        setAccounts(live);
        setSelectedId((prev) =>
          prev != null && live.some((a) => a.id === prev)
            ? prev
            : (live[0]?.id ?? null),
        );
      } catch (e) {
        if (!cancelled)
          setError(e instanceof Error ? e.message : "Failed to load accounts.");
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [authLoading, user]);

  const selected = accounts.find((a) => a.id === selectedId) ?? null;

  async function handleLinkSteam() {
    setLinking(true);
    setError("");
    try {
      const url = await getSteamLinkUrl();
      window.location.href = url;
    } catch {
      setError("Failed to start Steam linking.");
      setLinking(false);
    }
  }

  async function handleRemoveAccount() {
    if (!selected) return;
    if (!window.confirm(`Unlink ${selected.name} from Steam? Its matches are kept.`))
      return;
    setError("");
    try {
      await unlinkAccount(selected.id);
      const freshUser = await getMe();
      setUser(freshUser);
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to unlink account.");
    }
  }

  if (!authLoading && !user) {
    return (
      <>
        <Navbar />
        <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col items-center justify-center gap-3 px-4 py-8">
          <p className="text-lg font-medium">Log in to manage your settings</p>
          <a
            href="/login"
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light"
          >
            Go to login
          </a>
        </main>
      </>
    );
  }

  return (
    <>
      <Navbar />
      <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col px-4 py-8">
        <div className="my-auto flex flex-col gap-8">
          <section className="space-y-4">
            <h1 className="text-center text-xl font-semibold">Accounts</h1>

            {loading ? (
              <p className="py-8 text-center text-sm text-faint">
                Loading accounts…
              </p>
            ) : error ? (
              <p className="py-8 text-center text-sm text-danger">{error}</p>
            ) : accounts.length === 0 ? (
              <div className="mx-auto w-full max-w-4xl">
                <div className="rounded-xl bg-card px-5 py-6 text-center">
                  <h3 className="text-sm font-medium text-muted">
                    Link your Steam account to start tracking matches
                  </h3>
                  <button
                    type="button"
                    disabled={linking}
                    onClick={handleLinkSteam}
                    className="mt-4 inline-flex items-center gap-2 rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:opacity-50"
                  >
                    {linking ? "Redirecting to Steam..." : "Link Steam account"}
                  </button>
                </div>
              </div>
            ) : (
              <div className="flex flex-wrap justify-center gap-4">
                {accounts.map((account) => (
                  <AccountCard
                    key={account.id}
                    account={account}
                    selected={account.id === selectedId}
                    onSelect={() => setSelectedId(account.id)}
                  />
                ))}
              </div>
            )}
          </section>

          {selected && (
            <section className="space-y-4">
              <div className="flex items-baseline justify-between gap-4">
                <h2 className="text-xl font-semibold">{selected.name} ranks</h2>
                <span className="text-xs text-faint">
                  Wingman and Premier ranks are shared across all maps
                </span>
              </div>
              <div className="mx-auto w-full max-w-4xl space-y-3">
                {selected.premierRating != null && (
                  <RankRow label="Premier" hint="CS Rating - all modes pool">
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
                {selected.premierRating == null &&
                  selected.wingmanLevel == null &&
                  selected.competitiveRanks.length === 0 && (
                    <RankRow label="Unranked" hint="Play matches to get ranked">
                      <UnrankedBadge />
                    </RankRow>
                  )}
              </div>
            </section>
          )}

          {selected && (
            <section className="space-y-4">
              <h2 className="text-center text-xl font-semibold">
                Account settings
              </h2>
              <div className="mx-auto grid w-full max-w-4xl gap-2 sm:grid-cols-2 lg:grid-cols-4">
                <SettingsButton label="Reorder accounts" />
                <SettingsButton
                  label="Auth codes"
                  onClick={() => setAuthOpen(true)}
                />
                <SettingsButton
                  label="Add match manually"
                  onClick={() => setMatchOpen(true)}
                />
                <SettingsButton label="Remove account" danger onClick={handleRemoveAccount} />
              </div>
            </section>
          )}
        </div>
      </main>

      <AuthCodesModal
        account={selected}
        open={authOpen}
        onClose={() => setAuthOpen(false)}
        onSaved={refresh}
      />
      <AddMatchModal
        account={selected}
        open={matchOpen}
        onClose={() => setMatchOpen(false)}
        onAdded={refresh}
      />
    </>
  );
}

function SettingsButton({
  label,
  onClick,
  danger,
}: {
  label: string;
  onClick?: () => void;
  danger?: boolean;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`rounded-lg px-4 py-3 text-sm font-medium transition-colors ${
        danger
          ? "text-danger hover:bg-danger/10"
          : "text-muted hover:bg-hover hover:text-foreground"
      }`}
    >
      {label}
    </button>
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
