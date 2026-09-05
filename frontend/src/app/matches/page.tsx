"use client";

import Link from "next/link";
import { useCallback, useEffect, useRef, useState } from "react";
import { MatchDetailsModal } from "@/components/match-details-modal";
import { MatchHistory } from "@/components/match-history";
import { Navbar } from "@/components/navbar";
import { Pagination } from "@/components/pagination";
import { ReplayScannerPanel } from "@/components/replay-scanner-panel";
import {
  getAccountMatches,
  getMatchStatus,
  scanReplays,
  uploadDemo,
} from "@/lib/api";
import { MATCHES_PER_PAGE, MATCH_ROW_HEIGHT } from "@/lib/constants";
import { useAccounts } from "@/lib/use-accounts";
import type { Match } from "@/lib/types";

export default function MatchesPage() {
  const { accounts, activeAccountId, setActiveAccountId, loading: accountsLoading, error: accountsError } = useAccounts();
  const [matches, setMatches] = useState<Match[]>([]);
  const activeAccountIdRef = useRef(activeAccountId);
  const pollTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const refetchTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [uploading, setUploading] = useState(false);
  const [scanning, setScanning] = useState(false);
  const [detailsMatch, setDetailsMatch] = useState<Match | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);
  const fetchIdRef = useRef(0);
  const [page, setPage] = useState(1);

  const pageCount = Math.max(1, Math.ceil(matches.length / MATCHES_PER_PAGE));
  const currentPage = Math.min(page, pageCount);
  const pageMatches = matches.slice(
    (currentPage - 1) * MATCHES_PER_PAGE,
    currentPage * MATCHES_PER_PAGE,
  );

  useEffect(() => {
    activeAccountIdRef.current = activeAccountId;
  }, [activeAccountId]);

  const loadMatches = useCallback(async (accountId: number) => {
    const id = ++fetchIdRef.current;
    try {
      const live = await getAccountMatches(accountId);
      if (fetchIdRef.current !== id) return;
      setMatches(live);
      setError(null);
    } catch {
      if (fetchIdRef.current !== id) return;
      setError("Could not load matches.");
    }
  }, []);

  useEffect(() => {
    const run = async () => {
      await Promise.resolve();
      if (accountsLoading) return;
      if (!activeAccountId) {
        setMatches([]);
        setLoading(false);
        return;
      }
      setLoading(true);
      const id = ++fetchIdRef.current;
      try {
        const live = await getAccountMatches(activeAccountId);
        if (fetchIdRef.current !== id) return;
        setMatches(live);
        setError(null);
      } catch {
        if (fetchIdRef.current !== id) return;
        setMatches([]);
        setError("Could not load matches.");
      } finally {
        if (fetchIdRef.current === id) setLoading(false);
      }
    };
    void run();
  }, [accountsLoading, activeAccountId]);

  const hasUnscored = pageMatches.some(
    (m) => m.status === "Parsed" && !m.scoredAt,
  );

  useEffect(() => {
    if (!hasUnscored) return;

    const started = Date.now();
    if (refetchTimerRef.current) clearInterval(refetchTimerRef.current);
    refetchTimerRef.current = setInterval(async () => {
      if (Date.now() - started > 60_000) {
        if (refetchTimerRef.current) {
          clearInterval(refetchTimerRef.current);
          refetchTimerRef.current = null;
        }
        return;
      }
      const accountId = activeAccountIdRef.current;
      if (accountId != null) void loadMatches(accountId);
    }, 2_500);

    return () => {
      if (refetchTimerRef.current) {
        clearInterval(refetchTimerRef.current);
        refetchTimerRef.current = null;
      }
    };
  }, [hasUnscored, loadMatches]);

  const handleAccountSwitch = (accountId: number) => {
    setActiveAccountId(accountId);
    setPage(1);
  };

  useEffect(() => {
    return () => {
      if (pollTimerRef.current) {
        clearInterval(pollTimerRef.current);
        pollTimerRef.current = null;
      }
    };
  }, []);

  const pollMatchStatus = (matchId: string) => {
    const started = Date.now();
    if (pollTimerRef.current) clearInterval(pollTimerRef.current);
    pollTimerRef.current = setInterval(async () => {
      try {
        const status = await getMatchStatus(matchId);
        if (status.status !== "Pending" || Date.now() - started > 120_000) {
          if (pollTimerRef.current) {
            clearInterval(pollTimerRef.current);
            pollTimerRef.current = null;
          }
          if (status.status === "Failed") {
            setError(status.error ?? "Demo parsing failed.");
          }
          setUploading(false);
          if (activeAccountIdRef.current != null) void loadMatches(activeAccountIdRef.current);
        }
      } catch {
        if (pollTimerRef.current) {
          clearInterval(pollTimerRef.current);
          pollTimerRef.current = null;
        }
        setUploading(false);
        if (activeAccountIdRef.current != null) void loadMatches(activeAccountIdRef.current);
      }
    }, 2_000);
  };

  const handleUpload = async (file: File) => {
    if (!activeAccountId) {
      setError("Could not upload.");
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
      setError("Upload failed. Try again.");
    }
  };

  const handleScanReplays = async () => {
    if (scanning) return;
    setScanning(true);
    setError(null);
    try {
      await scanReplays();
      await new Promise((r) => setTimeout(r, 1500));
      const accountId = activeAccountIdRef.current;
      if (accountId != null) void loadMatches(accountId);
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not scan replays.");
    } finally {
      setScanning(false);
    }
  };

  return (
    <>
      <Navbar />
      <main className="mx-auto w-full max-w-6xl flex-1 space-y-6 px-4 py-8">
        {(error || accountsError) && (
          <div
            role="alert"
            className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger"
          >
            {error || accountsError}
          </div>
        )}

        {!accountsLoading && accounts.length === 0 ? (
          <div className="mx-auto flex w-full max-w-md flex-col items-center gap-4 rounded-xl bg-card px-5 py-10 text-center">
            <h2 className="text-lg font-medium">No accounts yet</h2>
            <p className="text-sm text-muted">
              Link your Steam account to start tracking matches, then upload .dem files here.
            </p>
            <Link
              href="/accounts"
              className="rounded-lg bg-primary px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light"
            >
              Link Steam account
            </Link>
          </div>
        ) : (
        <>
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

        <ReplayScannerPanel
          onMatchesChanged={() => {
            if (activeAccountIdRef.current != null) void loadMatches(activeAccountIdRef.current);
          }}
        />

        <section>
          <div className="flex items-center justify-between px-1 pb-4">
            <h1 className="font-semibold">Match history</h1>
            <div className="flex flex-wrap items-center gap-3">
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
                onClick={() => void handleScanReplays()}
                disabled={scanning}
                title="Scan replays folder"
                aria-label="Scan replays folder"
                className="relative flex size-9 items-center justify-center rounded-lg border border-border text-muted transition-colors hover:bg-hover hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50"
              >
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className={`size-4 ${scanning ? "animate-spin" : ""}`}
                  aria-hidden="true"
                >
                  <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8" />
                  <path d="M21 3v5h-5" />
                  <path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16" />
                  <path d="M3 21v-5h5" />
                </svg>
              </button>
              <button
                type="button"
                disabled={uploading}
                onClick={() => fileInputRef.current?.click()}
                className="rounded-lg border border-border px-3 py-2 text-sm text-muted transition-colors hover:bg-hover hover:text-foreground disabled:cursor-not-allowed disabled:opacity-50"
              >
                Upload .dem
              </button>
            </div>
          </div>

          {loading ? (
            <p className="px-1 py-8 text-center text-sm text-faint">Loading matches…</p>
          ) : matches.length === 0 ? (
            <p className="px-1 py-8 text-center text-sm text-faint">
              No matches yet - download a demo from CS2 and upload it here.
            </p>
          ) : (
            <div
              style={{ minHeight: MATCH_ROW_HEIGHT * MATCHES_PER_PAGE }}
            >
              <MatchHistory
                matches={pageMatches}
                onOpenDetails={(match) => {
                  if (match.status === "Parsed") setDetailsMatch(match);
                }}
              />
              <Pagination page={currentPage} pageCount={pageCount} onPageChange={setPage} />
            </div>
          )}
        </section>
        </>
        )}
      </main>

      <MatchDetailsModal
        key={detailsMatch?.id ?? "none"}
        match={detailsMatch}
        open={detailsMatch !== null}
        onClose={() => setDetailsMatch(null)}
        onFlagChanged={(matchId, hasFlaggedPlayer) => {
          setMatches((current) =>
            current.map((m) => (m.id === matchId ? { ...m, hasFlaggedPlayer } : m)),
          );
        }}
      />
    </>
  );
}
