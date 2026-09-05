"use client";

import { useEffect, useState } from "react";
import { Modal } from "@/components/modal";
import { getReplaySettings, updateReplaySettings } from "@/lib/api";
import type { ReplaySettings } from "@/lib/types";

export function ReplaysFolderModal({
  open,
  onClose,
  onSaved,
}: {
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [settings, setSettings] = useState<ReplaySettings | null>(null);
  const [path, setPath] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    getReplaySettings()
      .then((s) => {
        setSettings(s);
        setPath(s.hostPath);
      })
      .catch(() => setSettings(null));
  }, []);

  const handleBrowse = async () => {
    setError(null);
    setNotice(null);
    const picker = (
      window as unknown as {
        showDirectoryPicker?: () => Promise<{ name: string }>;
      }
    ).showDirectoryPicker;
    if (typeof picker !== "function") {
      setNotice(
        "This browser can't open a folder picker (Chrome/Edge can). Enter the path into the box above instead.",
      );
      return;
    }
    try {
      const handle = await picker();
      setNotice(
        `You picked "${handle.name}". Browsers don't expose the full path — copy it from the picker's address bar and paste it into the box above.`,
      );
    } catch (e) {
      if (e instanceof DOMException && e.name === "AbortError") return;
      setError("Could not open the folder picker.");
    }
  };

  const handleSave = async () => {
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
        const s = await getReplaySettings();
        setSettings(s);
        setPath(s.hostPath);
      } else if (result.restartRequired) {
        setNotice(
          "Docker restart required — run `docker compose up -d` once so the container picks up the new replays folder, then reload this page.",
        );
        setSettings((cur) =>
          cur ? { ...cur, hostPath: result.hostPath } : cur,
        );
      } else {
        setNotice("Replays path saved.");
        const s = await getReplaySettings();
        setSettings(s);
        setPath(s.hostPath);
      }
      onSaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save the replays path.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title="Replays folder" wide>
      <div className="space-y-4 text-sm">
        <p className="text-muted">
          Auto-scanning looks for `.dem` files in your CS2 replays folder every{" "}
          {settings?.scanIntervalMinutes ?? 20} minutes and attaches them to your
          linked accounts automatically. Set the folder once to enable it.
        </p>

        {settings?.hasPath && (
          <p className="text-faint">
            Current path: {settings.hostPath}.{" "}
            {settings.restartRequired
              ? "Restart required — run `docker compose up -d`."
              : "Active."}
          </p>
        )}

        {error && (
          <div
            role="alert"
            className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-danger"
          >
            {error}
          </div>
        )}
        {notice && (
          <div className="rounded-lg border border-border bg-hover px-4 py-2 text-muted">
            {notice}
          </div>
        )}

        <div className="flex gap-2">
          <input
            type="text"
            value={path}
            onChange={(e) => setPath(e.target.value)}
            placeholder="e.g. C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\csgo\replays"
            className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/40"
          />
          <button
            type="button"
            onClick={handleBrowse}
            className="rounded-lg border border-border px-4 py-2 text-sm font-medium text-muted transition-colors hover:bg-hover hover:text-foreground"
          >
            Browse…
          </button>
        </div>

        <p className="text-faint">
          Safari and Firefox fall back to entering the path manually.
        </p>

        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg px-4 py-2 text-sm font-medium text-muted transition-colors hover:bg-hover hover:text-foreground"
          >
            Close
          </button>
          <button
            type="button"
            onClick={handleSave}
            disabled={busy || !path.trim()}
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-50"
          >
            {busy ? "Saving…" : "Save"}
          </button>
        </div>
      </div>
    </Modal>
  );
}