"use client";

import { useState, type FormEvent } from "react";
import { Modal } from "./modal";

interface AuthCodeModalProps {
  open: boolean;
  onClose: () => void;
}

const tutorialSteps = [
  "Launch CS2 and open the Play menu.",
  "Select the Watch tab, then Your matches.",
  "Open your most recent match and click Share / copy the match code.",
  "Paste that code here — we use it to verify the account is yours.",
];

export function AuthCodeModal({ open, onClose }: AuthCodeModalProps) {
  const [view, setView] = useState<"form" | "tutorial">("form");
  const [code, setCode] = useState("");
  const [sent, setSent] = useState(false);
  const [confirmed, setConfirmed] = useState(false);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!code.trim()) return;
    setSent(true);
  }

  function handleClose() {
    setView("form");
    setCode("");
    setSent(false);
    setConfirmed(false);
    onClose();
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title={view === "form" ? "Authentication code" : "How to get the codes"}
    >
      {view === "tutorial" ? (
        <div className="space-y-5">
          <ol className="space-y-3">
            {tutorialSteps.map((step, index) => (
              <li key={step} className="flex gap-3 text-sm text-muted">
                <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-card text-xs font-semibold text-primary-light">
                  {index + 1}
                </span>
                <span className="pt-0.5">{step}</span>
              </li>
            ))}
          </ol>
          <button
            type="button"
            onClick={() => setView("form")}
            className="flex w-full items-center justify-center gap-2 rounded-lg border border-border px-4 py-2 text-sm font-medium text-muted transition-colors hover:bg-hover hover:text-foreground"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="size-4"
              aria-hidden
            >
              <path d="m12 19-7-7 7-7M19 12H5" />
            </svg>
            Back
          </button>
        </div>
      ) : (
        <div className="space-y-5">
          <p className="text-sm leading-relaxed text-muted">
            Insert the code of your most recent match so we can verify this
            account belongs to you.
          </p>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1">
              <label
                htmlFor="auth-code"
                className="text-sm font-medium text-muted"
              >
                Code of your most recent match
              </label>
              <input
                id="auth-code"
                value={code}
                onChange={(event) => {
                  setCode(event.target.value);
                  setSent(false);
                  setConfirmed(false);
                }}
                placeholder="CSGO-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX"
                autoFocus
                className="w-full rounded-lg border border-border bg-card px-4 py-2 font-mono text-sm placeholder:text-faint focus:border-primary focus:outline-none"
              />
            </div>

            {sent && !confirmed && (
              <p className="rounded-lg border border-success/40 bg-success/10 px-4 py-2 text-sm text-success">
                Code accepted — confirm to finish linking this account.
              </p>
            )}

            {!sent ? (
              <button
                type="submit"
                disabled={!code.trim()}
                className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-40"
              >
                Send
              </button>
            ) : (
              <div className="grid grid-cols-[auto_1fr_1fr] gap-2">
                <button
                  type="button"
                  onClick={() => setView("tutorial")}
                  title="How do I get the codes?"
                  aria-label="Help"
                  className="flex size-9 items-center justify-center rounded-lg border border-border font-bold text-muted transition-colors hover:bg-hover hover:text-primary-light"
                >
                  ?
                </button>
                <button
                  type="button"
                  onClick={handleClose}
                  aria-label="Cancel"
                  className="flex items-center justify-center rounded-lg border border-danger/50 bg-danger/10 font-bold text-danger transition-colors hover:bg-danger/20"
                >
                  ✕
                </button>
                <button
                  type="button"
                  onClick={() => setConfirmed(true)}
                  aria-label="Confirm"
                  className={`flex items-center justify-center rounded-lg font-bold transition-colors ${
                    confirmed
                      ? "border border-success/50 bg-success/10 text-success"
                      : "bg-success text-deep hover:opacity-90"
                  }`}
                >
                  ✓
                </button>
              </div>
            )}
          </form>
        </div>
      )}
    </Modal>
  );
}
