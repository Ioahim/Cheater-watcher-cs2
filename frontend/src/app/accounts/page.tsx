"use client";

import { useCallback, useEffect, useState } from "react";
import { AccountCard } from "@/components/account-card";
import { Modal } from "@/components/modal";
import { Navbar } from "@/components/navbar";
import {
  CompetitiveBadge,
  PremierBadge,
  UnrankedBadge,
  WingmanBadge,
} from "@/components/rank-badge";
import { exchangeSteamCode, getSteamLinkUrl, reorderAccounts, unlinkAccount } from "@/lib/api";
import type { Account } from "@/lib/types";
import { useAccounts } from "@/lib/use-accounts";

export default function AccountsPage() {
  const { accounts, activeAccountId, setActiveAccountId, loading, error, refresh } = useAccounts();
  const [linking, setLinking] = useState(false);
  const [localError, setLocalError] = useState("");
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [reorderMode, setReorderMode] = useState(false);
  const [pendingOrder, setPendingOrder] = useState<Account[]>([]);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  const displayAccounts = reorderMode ? pendingOrder : accounts;
  const selected = accounts.find((a) => a.id === activeAccountId) ?? null;
  const errorMsg = localError || error;

  function enterReorderMode() {
    setPendingOrder([...accounts]);
    setReorderMode(true);
  }

  function cancelReorder() {
    setReorderMode(false);
    setPendingOrder([]);
    setDragIndex(null);
    setDragOverIndex(null);
  }

  async function saveReorder() {
    setLocalError("");
    try {
      await reorderAccounts(pendingOrder.map((a) => a.id));
      await refresh();
      setReorderMode(false);
      setPendingOrder([]);
    } catch {
      setLocalError("Could not reorder accounts.");
    }
  }

  const handleDragStart = useCallback((index: number) => (e: React.DragEvent) => {
    setDragIndex(index);
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", String(index));
  }, []);

  const handleDragOver = useCallback((index: number) => (e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
    setDragOverIndex(index);
  }, []);

  const handleDrop = useCallback((dropIndex: number) => async (e: React.DragEvent) => {
    e.preventDefault();
    const fromIndex = dragIndex;
    setDragIndex(null);
    setDragOverIndex(null);
    if (fromIndex === null || fromIndex === dropIndex) return;

    setPendingOrder((current) => {
      const reordered = [...current];
      const [moved] = reordered.splice(fromIndex, 1);
      reordered.splice(dropIndex, 0, moved);
      return reordered;
    });
  }, [dragIndex]);

  const handleDragEnd = useCallback(() => {
    setDragIndex(null);
    setDragOverIndex(null);
  }, []);

  async function handleLinkSteam() {
    setLinking(true);
    setLocalError("");
    try {
      const url = await getSteamLinkUrl();
      window.location.href = url;
    } catch {
      setLocalError("Failed to start Steam linking.");
      setLinking(false);
    }
  }

  function openRemoveConfirm() {
    if (!selected) return;
    setConfirmOpen(true);
  }

  async function confirmRemove() {
    if (!selected) return;
    setConfirmOpen(false);
    setLocalError("");
    try {
      await unlinkAccount(selected.id);
      await refresh();
    } catch (e) {
      setLocalError(e instanceof Error ? e.message : "Failed to unlink account.");
    }
  }

  useEffect(() => {
    const run = async () => {
      await Promise.resolve();
      const hash = window.location.hash;
      if (!hash) return;
      window.history.replaceState(null, "", window.location.pathname + window.location.search);

      const codeMatch = hash.match(/#steam_code=([^&]+)/);
      if (codeMatch) {
        setLinking(true);
        setLocalError("");
        try {
          await exchangeSteamCode(codeMatch[1]);
          await refresh();
          setLinking(false);
        } catch {
          setLinking(false);
          setLocalError("Steam linking failed. Try again.");
        }
        return;
      }

      const errorMatch = hash.match(/#steam=(expired|failed)/);
      if (errorMatch) {
        setLocalError(
          errorMatch[1] === "expired"
            ? "Steam login expired. Try again."
            : "Steam linking failed. Try again.",
        );
      }
    };
    void run();
  }, [refresh]);

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
            ) : errorMsg ? (
              <p className="py-8 text-center text-sm text-danger">{errorMsg}</p>
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
                {displayAccounts.map((account, index) => (
                  <AccountCard
                    key={account.id}
                    account={account}
                    selected={account.id === activeAccountId}
                    onSelect={() => setActiveAccountId(account.id)}
                    draggable={reorderMode}
                    reorderMode={reorderMode}
                    dragOver={dragOverIndex === index}
                    onDragStart={handleDragStart(index)}
                    onDragOver={handleDragOver(index)}
                    onDrop={handleDrop(index)}
                    onDragEnd={handleDragEnd}
                  />
                ))}
                {!reorderMode && (
                  <button
                    type="button"
                    disabled={linking}
                    onClick={handleLinkSteam}
                    className="flex w-40 flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed border-border px-4 py-5 text-muted transition-colors hover:bg-hover hover:text-foreground disabled:opacity-50"
                  >
                    <span className="flex size-12 items-center justify-center rounded-full border-2 border-dashed border-current text-lg">+</span>
                    <span className="text-sm font-medium">
                      {linking ? "Redirecting..." : "Link account"}
                    </span>
                  </button>
                )}
              </div>
            )}
          </section>

          {selected && (
            <section className="space-y-4">
              <div className="flex items-baseline justify-between gap-4">
                <h2 className="text-xl font-semibold">{selected.name} ranks</h2>
              </div>
              <div className="mx-auto w-full max-w-4xl space-y-3">
                {selected.premierRating != null && (
                  <RankRow label="Premier">
                    <PremierBadge rating={selected.premierRating} />
                  </RankRow>
                )}
                {selected.wingmanLevel != null && (
                  <RankRow label="Wingman">
                    <WingmanBadge level={selected.wingmanLevel} />
                  </RankRow>
                )}
                {selected.competitiveRanks.length > 0 && (
                  <div className="space-y-3 rounded-xl bg-card p-4">
                    <p className="text-sm font-medium text-muted">Competitive</p>
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
                    <RankRow label="Unranked">
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
              {reorderMode ? (
                <div className="flex w-full justify-center gap-2">
                  <SettingsButton label="Save order" onClick={saveReorder} />
                  <SettingsButton label="Cancel" onClick={cancelReorder} />
                </div>
              ) : (
                <div className="mx-auto grid w-full max-w-2xl gap-2 sm:grid-cols-2">
                  <SettingsButton label="Reorder accounts" onClick={enterReorderMode} />
                  <SettingsButton label="Remove account" danger onClick={openRemoveConfirm} />
                </div>
              )}
            </section>
          )}
        </div>
      </main>

      <Modal
        open={confirmOpen}
        onClose={() => setConfirmOpen(false)}
        title="Remove account"
      >
        <p className="text-sm text-muted">
          Unlink <span className="font-medium text-foreground">{selected?.name}</span> from Steam? Its matches are kept.
        </p>
        <div className="mt-5 flex justify-end gap-3">
          <button
            type="button"
            onClick={() => setConfirmOpen(false)}
            className="rounded-lg px-4 py-2 text-sm font-medium text-muted transition-colors hover:bg-hover hover:text-foreground"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={confirmRemove}
            className="rounded-lg bg-danger px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-danger/80"
          >
            Remove
          </button>
        </div>
      </Modal>
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
  children,
}: {
  label: string;
  children: React.ReactNode;
}) {
  return (
    <div className="flex items-center justify-between rounded-xl bg-card px-4 py-3">
      <div>
        <p className="text-sm font-medium">{label}</p>
      </div>
      {children}
    </div>
  );
}
