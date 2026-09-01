"use client";

import { useState, type FormEvent } from "react";
import { Modal } from "./modal";
import { updateCredentials } from "@/lib/api";
import type { Account } from "@/lib/types";

interface AuthCodesModalProps {
  account: Account | null;
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
}

export function AuthCodesModal({
  account,
  open,
  onClose,
  onSaved,
}: AuthCodesModalProps) {
  const [authCode, setAuthCode] = useState("");
  const [shareCode, setShareCode] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState<{ text: string; tone: "success" | "warning" } | null>(null);

  function handleClose() {
    setAuthCode("");
    setShareCode("");
    setSaving(false);
    setError("");
    setNotice(null);
    onClose();
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!account || !authCode.trim()) return;
    setSaving(true);
    setError("");
    setNotice(null);
    try {
      const result = await updateCredentials(
        account.id,
        null,
        authCode.trim(),
        shareCode.trim() || null,
      );
      if (!shareCode.trim()) {
        setNotice({ text: "Credentials saved.", tone: "success" });
      } else if (result) {
        switch (result.status) {
          case "ingested":
            setNotice({ text: "Queued for parsing", tone: "success" });
            break;
          case "duplicate":
            setNotice({ text: "This match was already added", tone: "warning" });
            break;
          case "invalid":
            setNotice({
              text: "This share code is invalid or couldn't be decoded",
              tone: "warning",
            });
            break;
          case "download_failed":
            setNotice({
              text: "Could not download the demo. Check the code and try again.",
              tone: "warning",
            });
            break;
        }
      }
      onSaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to save credentials.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={account ? `Auth codes - ${account.name}` : "Auth codes"}
    >
      <div className="space-y-5">
        <p className="text-sm leading-relaxed text-muted">
          Steam mails the auth (consent) code together with a recent match code.
          Paste both below to enable automatic tracking and ingest that match.
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
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
                setNotice(null);
              }}
              placeholder="Paste your auth code"
              autoFocus
              className="w-full rounded-lg border border-border bg-card px-4 py-2 font-mono text-sm placeholder:text-faint focus:border-primary focus:outline-none"
            />
          </div>

          <div className="space-y-1">
            <label
              htmlFor="share-code"
              className="text-sm font-medium text-muted"
            >
              Recent match code
            </label>
            <input
              id="share-code"
              value={shareCode}
              onChange={(e) => {
                setShareCode(e.target.value);
                setNotice(null);
              }}
              placeholder="CSGO-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX"
              className="w-full rounded-lg border border-border bg-card px-4 py-2 font-mono text-sm placeholder:text-faint focus:border-primary focus:outline-none"
            />
            <p className="text-xs text-faint">
              The match this code points to will be ingested automatically and
              used as the tracking cursor.
            </p>
          </div>

          {error && (
            <p className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-sm text-danger">
              {error}
            </p>
          )}

          {notice && (
            <p
              className={
                notice.tone === "success"
                  ? "rounded-lg border border-success/40 bg-success/10 px-4 py-2 text-sm text-success"
                  : "rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-sm text-danger"
              }
            >
              {notice.text}
            </p>
          )}

          <button
            type="submit"
            disabled={!authCode.trim() || saving}
            className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-40"
          >
            {saving ? "Saving..." : "Save codes"}
          </button>
        </form>
      </div>
    </Modal>
  );
}
