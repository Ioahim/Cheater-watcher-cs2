"use client";

import { useState, type FormEvent } from "react";
import { Modal } from "./modal";
import { addShareCode } from "@/lib/api";
import type { Account } from "@/lib/types";

interface AddMatchModalProps {
  account: Account | null;
  open: boolean;
  onClose: () => void;
  onAdded: () => void;
}

export function AddMatchModal({
  account,
  open,
  onClose,
  onAdded,
}: AddMatchModalProps) {
  const [code, setCode] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState("");

  function handleClose() {
    setCode("");
    setSubmitting(false);
    setError("");
    setDone("");
    onClose();
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!account || !code.trim()) return;
    setSubmitting(true);
    setError("");
    setDone("");
    try {
      const result = await addShareCode(account.id, code.trim());
      if (result.status === "ingested") {
        setDone("Match added and queued for parsing.");
        onAdded();
      } else if (result.status === "duplicate") {
        setDone("That match was already added — each match can only be added once.");
      } else if (result.status === "invalid") {
        setError("Invalid share code. Check the format and try again.");
      } else {
        setError("Could not download that match. Try again later.");
      }
    } catch (e) {
      setError(e instanceof Error ? e.message : "Failed to add match.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={account ? `Add match — ${account.name}` : "Add match manually"}
    >
      <div className="space-y-5">
        <p className="text-sm leading-relaxed text-muted">
          Paste any match sharing code to add it. Every match can only be added
          once.
        </p>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <label
              htmlFor="match-code"
              className="text-sm font-medium text-muted"
            >
              Match sharing code
            </label>
            <input
              id="match-code"
              value={code}
              onChange={(e) => {
                setCode(e.target.value);
                setError("");
                setDone("");
              }}
              placeholder="CSGO-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX"
              autoFocus
              className="w-full rounded-lg border border-border bg-card px-4 py-2 font-mono text-sm placeholder:text-faint focus:border-primary focus:outline-none"
            />
          </div>

          {error && (
            <p className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-sm text-danger">
              {error}
            </p>
          )}

          {done && (
            <p className="rounded-lg border border-success/40 bg-success/10 px-4 py-2 text-sm text-success">
              {done}
            </p>
          )}

          <button
            type="submit"
            disabled={!code.trim() || submitting}
            className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-40"
          >
            {submitting ? "Adding..." : "Add match"}
          </button>
        </form>
      </div>
    </Modal>
  );
}
