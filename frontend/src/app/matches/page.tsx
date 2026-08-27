"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { AuthBanner } from "@/components/auth-banner";
import { MatchDetailsModal } from "@/components/match-details-modal";
import { MatchHistory } from "@/components/match-history";
import { Navbar } from "@/components/navbar";
import { Pagination } from "@/components/pagination";
import {
  getAccountMatches,
  getAccounts,
  getMatchStatus,
  setMatchFlag,
  uploadDemo,
} from "@/lib/api";
import { mockAccounts, mockMatches } from "@/lib/mock-data";
import type { Account, Match } from "@/lib/types";

export default function MatchesPage() {
  const [accounts, setAccounts] = useState<Account[]>(mockAccounts);
  const [activeAccountId, setActiveAccountId] = useState<number>(mockAccounts[0].id);
  const [matches, setMatches] = useState<Match[]>(mockMatches);
  const [usingLiveData, setUsingLiveData] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [detailsMatch, setDetailsMatch] = useState<Match | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const fetchIdRef = useRef(0);

  const loadMatches = useCallback(async (accountId: number) => {
    try {
      const live = await getAccountMatches(accountId);
      setMatches(live);
      setUsingLiveData(true);
      setError(null);
    } catch {
      if (!usingLiveData) {
        setMatches(mockMatches);
      }
    }
  }, [usingLiveData]);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      try {
        const liveAccounts = await getAccounts();
        if (cancelled) return;
        setAccounts(liveAccounts);
        setActiveAccountId(liveAccounts[0]?.id ?? 0);
        setUsingLiveData(true);
        setError(null);
        await loadMatches(liveAccounts[0].id);
      } catch {
        if (!cancelled) {
          setUsingLiveData(false);
          setMatches(mockMatches);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleAccountSwitch = async (accountId: number) => {
    setActiveAccountId(accountId);
    if (usingLiveData) {
      setLoading(true);
      const id = ++fetchIdRef.current;
      try {
        const live = await getAccountMatches(accountId);
        if (fetchIdRef.current === id) {
          setMatches(live);
          setUsingLiveData(true);
          setError(null);
        }
      } catch {
        if (fetchIdRef.current === id) {
          if (!usingLiveData) setMatches(mockMatches);
        }
      } finally {
        if (fetchIdRef.current === id) setLoading(false);
      }
    }
  };

  const handleToggleFlag = async (match: Match) => {
    const nextFlagged = !match.flagged;
    setMatches((current) =>
      current.map((m) => (m.id === match.id ? { ...m, flagged: nextFlagged } : m)),
    );
    if (!usingLiveData) return;
    try {
      await setMatchFlag(match.id, nextFlagged);
    } catch {
      setMatches((current) =>
        current.map((m) => (m.id === match.id ? { ...m, flagged: !nextFlagged } : m)),
      );
      setError("Could not update flag — is the backend running?");
    }
  };

  const pollMatchStatus = (matchId: string) => {
    const started = Date.now();
    const timer = setInterval(async () => {
      try {
        const status = await getMatchStatus(matchId);
        if (status.status !== "Pending" || Date.now() - started > 120_000) {
          clearInterval(timer);
          if (status.status === "Failed") {
            setError(status.error ?? "Demo parsing failed.");
          }
          setUploading(false);
          await loadMatches(activeAccountId);
        }
      } catch {
        clearInterval(timer);
        setUploading(false);
      }
    }, 2_000);
  };

  const handleUpload = async (file: File) => {
    if (!usingLiveData || !activeAccountId) {
      setError("Upload requires the backend to be running.");
      return;
    }
    setUploading(true);
    setError(null);
    try {
      const result = await uploadDemo(activeAccountId, file);
      if (!result.duplicate) {
        pollMatchStatus(result.matchId);
      } else {
        setUploading(false);
      }
    } catch {
      setUploading(false);
      setError("Upload failed — check the file is a valid .dem under 500 MB.");
    }
  };

  return (
    <>
      <Navbar />
      <main className="mx-auto w-full max-w-6xl flex-1 space-y-6 px-4 py-8">
        <AuthBanner />

        {error && (
          <div
            role="alert"
            className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger"
          >
            {error}
          </div>
        )}

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
          <Link
            href="/accounts"
            aria-label="Add account"
            title="Add account"
            className="flex size-9 items-center justify-center rounded-lg text-sm text-muted transition-colors hover:bg-hover hover:text-foreground"
          >
            +
          </Link>
        </div>

        <section>
          <div className="flex items-center justify-between px-1 pb-4">
            <h1 className="font-semibold">Match history</h1>
            <div className="flex items-center gap-4">
              {uploading && (
                <span className="text-xs text-primary-light">Processing demo…</span>
              )}
              <input
                ref={fileInputRef}
                type="file"
                accept=".dem"
                className="hidden"
                onChange={(event) => {
                  const file = event.target.files?.[0];
                  event.target.value = "";
                  if (file) void handleUpload(file);
                }}
              />
              <button
                type="button"
                disabled={uploading}
                onClick={() => fileInputRef.current?.click()}
                className="rounded-lg border border-border px-3 py-2 text-sm text-muted transition-colors hover:bg-hover hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50"
              >
                Upload .dem
              </button>
              <Link
                href="/accounts"
                className="text-sm text-muted transition-colors hover:text-primary-light"
              >
                Add match manually →
              </Link>
            </div>
          </div>

          {loading ? (
            <p className="px-1 py-8 text-center text-sm text-faint">Loading matches…</p>
          ) : matches.length === 0 ? (
            <p className="px-1 py-8 text-center text-sm text-faint">
              No parsed matches yet — upload a .dem or connect a share code.
            </p>
          ) : (
            <MatchHistory
              matches={matches}
              onToggleFlag={handleToggleFlag}
              onOpenDetails={(match) => setDetailsMatch(match)}
            />
          )}
          <Pagination />
        </section>

        <footer className="text-center text-xs text-faint">
          Powered by{" "}
          <a
            href="https://leetify.com"
            target="_blank"
            rel="noreferrer"
            className="transition-colors hover:text-primary-light"
          >
            Leetify
          </a>
        </footer>
      </main>

      <MatchDetailsModal
        key={detailsMatch?.id ?? "none"}
        match={detailsMatch}
        open={detailsMatch !== null}
        onClose={() => setDetailsMatch(null)}
      />
    </>
  );
}
