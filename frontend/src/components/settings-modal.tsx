"use client";

import { useState, type FormEvent } from "react";
import { useAuth } from "@/components/auth-provider";
import { Modal } from "@/components/modal";
import {
  getSteamLinkUrl,
  updateCredentials,
} from "@/lib/api";

interface SettingsModalProps {
  open: boolean;
  onClose: () => void;
}

export function SettingsModal({ open, onClose }: SettingsModalProps) {
  const { user } = useAuth();
  const [authCode, setAuthCode] = useState("");
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [linking, setLinking] = useState(false);
  const [error, setError] = useState("");

  if (!user) return null;

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

  async function handleSaveCredentials(event: FormEvent) {
    event.preventDefault();
    if (!user!.ownAccountId || !authCode.trim()) return;
    setSaving(true);
    setSaved(false);
    setError("");
    try {
      await updateCredentials(user!.ownAccountId, null, authCode.trim());
      setSaved(true);
    } catch {
      setError("Failed to save credentials.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal open={open} onClose={onClose} title="Settings" wide>
      <div className="space-y-5">
        <section className="space-y-2">
          <h3 className="text-sm font-medium text-muted">Profile</h3>
          <div className="rounded-lg bg-card px-4 py-3">
            <p className="text-sm font-medium">{user.username}</p>
            <p className="mt-1 text-xs text-faint">
              {user.steam64Id
                ? `Steam linked (${user.steam64Id})`
                : "No Steam account linked"}
            </p>
          </div>

          {!user.steam64Id && (
            <button
              type="button"
              disabled={linking}
              onClick={handleLinkSteam}
              className="w-full rounded-lg bg-card px-4 py-2 text-sm font-medium text-muted transition-colors hover:bg-hover hover:text-foreground disabled:opacity-50"
            >
              {linking ? "Redirecting to Steam..." : "Link Steam account"}
            </button>
          )}

          {user.steam64Id && user.ownAccountId && (
            <p className="rounded-lg border border-success/40 bg-success/10 px-4 py-2 text-sm text-success">
              Steam account linked.
            </p>
          )}
        </section>

        {user.steam64Id && user.ownAccountId && (
          <section className="space-y-2">
            <h3 className="text-sm font-medium text-muted">CS2 Tracking</h3>
            <form onSubmit={handleSaveCredentials} className="space-y-3">
              <div className="space-y-1">
                <label
                  htmlFor="auth-code"
                  className="text-sm font-medium text-muted"
                >
                  Auth code
                </label>
                <input
                  id="auth-code"
                  type="password"
                  value={authCode}
                  onChange={(e) => {
                    setAuthCode(e.target.value);
                    setSaved(false);
                  }}
                  placeholder="Paste your GC consent auth code"
                  className="w-full rounded-lg border border-border bg-card px-4 py-2 font-mono text-sm placeholder:text-faint focus:border-primary focus:outline-none"
                />
              </div>

              {error && (
                <p className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-sm text-danger">
                  {error}
                </p>
              )}

              {saved && (
                <p className="rounded-lg border border-success/40 bg-success/10 px-4 py-2 text-sm text-success">
                  Credentials saved.
                </p>
              )}

              <button
                type="submit"
                disabled={!authCode.trim() || saving}
                className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-40"
              >
                {saving ? "Saving..." : "Save credentials"}
              </button>
            </form>
          </section>
        )}
      </div>
    </Modal>
  );
}
