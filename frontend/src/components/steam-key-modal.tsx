"use client";

import { useEffect, useState } from "react";
import { Modal } from "@/components/modal";
import { getSteamKeyStatus, saveSteamApiKey } from "@/lib/api";
import type { SteamKeyStatus } from "@/lib/types";

export function SteamKeyModal({
  open,
  onClose,
  onSaved,
}: {
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [status, setStatus] = useState<SteamKeyStatus | null>(null);
  const [key, setKey] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    getSteamKeyStatus()
      .then(setStatus)
      .catch(() => setStatus(null));
  }, []);

  const handleSave = async () => {
    if (!key.trim()) return;
    setBusy(true);
    setError(null);
    setNotice(null);
    try {
      const result = await saveSteamApiKey(key.trim());
      if (!result.saved || !result.canWriteEnv) {
        setError(
          "Could not write to .env. Set STEAM_WEB_API_KEY manually in your .env file and restart the stack.",
        );
      } else if (!result.checked) {
        setNotice(
          "Saved, but Steam couldn't be reached to verify the key. If it's wrong, Steam features just stay off.",
        );
      } else if (result.restartRequired) {
        setNotice(
          "Saved. Restart the app: run `docker compose up -d`, wait for the backend, then reload this page.",
        );
      } else {
        setNotice("Saved and already active.");
      }
      onSaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Could not save the Steam API key.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} onClose={onClose} title="Steam API key" wide>
      <div className="space-y-4 text-sm">
        <p className="text-muted">
          A free Steam <span className="font-medium text-foreground">Web API key</span>{" "}
          enables:
        </p>
        <ul className="list-disc space-y-1 pl-5 text-muted">
          <li>Persona names and avatars for linked accounts.</li>
          <li>VAC ban checks when you flag a player as Cheating or Suspicious.</li>
        </ul>
        <p className="text-muted">
          Without it the app still works, but those two features stay off.
        </p>
        <p className="text-muted">
          Get one (free) at{" "}
          <a
            href="https://steamcommunity.com/dev/apikey"
            target="_blank"
            rel="noreferrer"
            className="font-medium text-primary underline underline-offset-2 hover:text-primary-light"
          >
            steamcommunity.com/dev/apikey
          </a>{" "}
          — paste it below, save, and restart the app once.
        </p>

        {status?.configured && (
          <p className="text-faint">
            Key currently in .env: {status.keyHint}.{" "}
            {status.restartRequired || !status.active
              ? "Not active until you restart."
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

        <input
          type="password"
          value={key}
          onChange={(e) => setKey(e.target.value)}
          placeholder="Paste your Steam Web API key"
          className="w-full rounded-md border border-border bg-background px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-primary/40"
        />

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
            disabled={busy || !key.trim()}
            className="rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-50"
          >
            {busy ? "Saving…" : "Save key"}
          </button>
        </div>
      </div>
    </Modal>
  );
}