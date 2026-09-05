"use client";

import { useCallback, useEffect, useState } from "react";
import {
  dismissPendingReplay,
  getPendingReplays,
  getReplaySettings,
  resolvePendingReplay,
  updateReplaySettings,
} from "@/lib/api";
import { useAccounts } from "@/lib/use-accounts";
import type { PendingReplay, ReplaySettings } from "@/lib/types";

export function ReplayScannerPanel({
  onMatchesChanged,
}: {
  onMatchesChanged?: () => void;
}) {
  const { accounts } = useAccounts();
  const [settings, setSettings] = useState<ReplaySettings | null>(null);
  const [pending, setPending] = useState<PendingReplay[]>([]);
  const [path, setPath] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [selected, setSelected] = useState<Record<string, number>>({});

  const refresh = useCallback(async () => {
    try {
      const [s, p] = await Promise.all([getReplaySettings(), getPendingReplays()]);
      setSettings(s);
      setPending(p);
      setPath(s.hostPath);
      setError(null);
    } catch {
      setError("Could not load replay scanner status.");
    }
  }, []);

  useEffect(() => {
    const run = async () => {
      await Promise.resolve();
      void refresh();
    };
    void run();
  }, [refresh]);

  const handleSavePath = async () => {
    if (!path.trim()) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const result = await updateReplaySettings(path);
      if (!result.saved || !result.canWriteEnv) {
        setNotice(
          "Path saved, but the container couldn't write it to .env. Set STEAM_REPLAYS_ROOT manually in docker-compose and restart.",
        );
        await refresh();
      } else if (result.restartRequired) {
        setNotice(
          "Docker restart required — run `docker compose up -d` once so the container picks up the new replays folder, then reload this page.",
        );
        setSettings((cur) =>
          cur ? { ...cur, hostPath: result.hostPath } : cur,
        );
      } else {
        setNotice("Replays path saved.");
        await refresh();
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save the replays path.");
    } finally {
      setBusy(false);
    }
  };

  const handleResolve = async (p: PendingReplay, accountId: number) => {
    setBusy(true);
    setError(null);
    try {
      await resolvePendingReplay(p.id, accountId);
      await refresh();
      onMatchesChanged?.();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not attribute replay.");
    } finally {
      setBusy(false);
    }
  };

  const handleDismiss = async (p: PendingReplay) => {
    setBusy(true);
    setError(null);
    try {
      await dismissPendingReplay(p.id);
      await refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not dismiss replay.");
    } finally {
      setBusy(false);
    }
  };

  const showLastScan =
    settings?.lastScanAt != null || (settings?.lastScanNew ?? 0) > 0;

  return (
    <section className="space-y-4 rounded-xl border border-border bg-card p-4">
      {error && (
        <div role="alert" className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-sm text-danger">
          {error}
        </div>
      )}
      {notice && (
        <div className="rounded-lg border border-border bg-hover px-4 py-2 text-sm text-muted">{notice}</div>
      )}

      {!settings?.hasPath && (
        <div className="space-y-2 text-sm">
          <h3 className="font-semibold">Replay scanning</h3>
          <p className="text-muted">
            Auto-scanning looks for `.dem` files in your CS2 replays folder every{" "}
            {settings?.scanIntervalMinutes ?? 20} minutes and attaches them to your
            linked accounts automatically. Set your replays folder once to enable it.
          </p>
          <div className="flex gap-2">
            <input
              type="text"
              value={path}
              onChange={(e) => setPath(e.target.value)}
              placeholder="e.g. C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\replays"
              className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm"
            />
            <button
              type="button"
              onClick={handleSavePath}
              disabled={busy || !path.trim()}
              className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-50"
            >
              {busy ? "Saving…" : "Save"}
            </button>
          </div>
        </div>
      )}

      {showLastScan && (
        <p className="text-xs text-faint">
          {settings?.lastScanAt
            ? `Last scan: ${new Date(settings.lastScanAt).toLocaleString()} — `
            : "Last scan: — "}
          {settings?.lastScanNew ?? 0} new, {settings?.lastScanAttributed ?? 0} attributed,{" "}
          {settings?.lastScanPending ?? 0} pending
        </p>
      )}
      {settings?.lastScanError && (
        <p className="text-xs text-danger">{settings.lastScanError}</p>
      )}

      {pending.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-sm font-semibold">Replays awaiting a decision</h3>
            {pending.map((p) => (
              <div key={p.id} className="space-y-2 rounded-lg border border-border bg-background p-3">
                <div className="flex items-center justify-between gap-2">
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{p.fileName}</p>
                    <p className="text-xs text-muted">
                      {p.mapName} · {p.mode} · {new Date(p.discoveredAt).toLocaleString()}
                    </p>
                  </div>
                  <div className="flex flex-shrink-0 items-center gap-2">
                    <select
                      value={selected[p.id] ?? p.linkedAccountOptions[0] ?? accounts[0]?.id ?? ""}
                      onChange={(e) =>
                        setSelected((cur) => ({ ...cur, [p.id]: Number(e.target.value) }))
                      }
                      disabled={busy}
                      className="rounded-md border border-border bg-background px-2 py-1 text-sm"
                    >
                      {accounts.map((a) => (
                        <option key={a.id} value={a.id}>
                          {a.name}
                        </option>
                      ))}
                    </select>
                    <button
                      type="button"
                      onClick={() =>
                        handleResolve(
                          p,
                          selected[p.id] ?? p.linkedAccountOptions[0] ?? accounts[0]?.id ?? 0,
                        )
                      }
                      disabled={busy || accounts.length === 0}
                      className="rounded-lg bg-primary px-3 py-1 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:opacity-50"
                    >
                      Attribute
                    </button>
                    <button
                      type="button"
                      onClick={() => handleDismiss(p)}
                      disabled={busy}
                      className="rounded-lg border border-border px-3 py-1 text-sm text-muted transition-colors hover:bg-hover disabled:opacity-50"
                    >
                      Ignore
                    </button>
                  </div>
                </div>
                <p className="text-xs text-faint">
                  {p.players
                    .map((pl) => (pl.linked ? `${pl.name} (linked)` : pl.name))
                    .join(", ")}
                </p>
              </div>
            ))}
        </div>
      )}
    </section>
  );
}
