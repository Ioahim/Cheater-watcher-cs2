"use client";

import { useState, type FormEvent } from "react";
import { Modal } from "./modal";

interface MatchCodeModalProps {
  open: boolean;
  onClose: () => void;
}

export function MatchCodeModal({ open, onClose }: MatchCodeModalProps) {
  const [code, setCode] = useState("");
  const [submitted, setSubmitted] = useState(false);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!code.trim()) return;
    setSubmitted(true);
  }

  function handleClose() {
    setCode("");
    setSubmitted(false);
    onClose();
  }

  return (
    <Modal
      open={open}
      onClose={handleClose}
      title="Insert the match code"
    >
      {submitted ? (
        <div className="space-y-5">
          <p className="flex items-center gap-2 rounded-lg border border-success/40 bg-success/10 px-4 py-3 text-sm text-success">
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="size-4 shrink-0"
              aria-hidden
            >
              <path d="M20 6 9 17l-5-5" />
            </svg>
            Match added successfully.
          </p>
          <button
            type="button"
            onClick={handleClose}
            className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light"
          >
            Done
          </button>
        </div>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <label
              htmlFor="match-code"
              className="text-sm font-medium text-muted"
            >
              Code
            </label>
            <input
              id="match-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              placeholder="CSGO-XXXXX-XXXXX-XXXXX-XXXXX-XXXXX"
              autoFocus
              className="w-full rounded-lg border border-border bg-card px-4 py-2 font-mono text-sm placeholder:text-faint focus:border-primary focus:outline-none"
            />
          </div>
          <button
            type="submit"
            disabled={!code.trim()}
            className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-40"
          >
            Send
          </button>
        </form>
      )}
    </Modal>
  );
}
