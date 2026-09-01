"use client";

import Image from "next/image";
import { useEffect, useRef, useState } from "react";
import { getMatchPlayers, setPlayerFlag } from "@/lib/api";
import { FLAG_REASONS } from "@/lib/types";
import type { Match, MatchPlayerRow, MatchRoster } from "@/lib/types";
import { Modal } from "./modal";
import { PlayerDetailModal } from "./player-detail-modal";
import { RankBadge } from "./rank-badge";

function TeamSection({
  side,
  icon,
  label,
  players,
  matchId,
  onFlagToggled,
  onOpenPlayer,
}: {
  side: string;
  icon: string;
  label: string;
  players: MatchPlayerRow[];
  matchId: string;
  onFlagToggled: (player: MatchPlayerRow, flagged: boolean, reason?: number, note?: string) => void;
  onOpenPlayer: (steam64Id: string) => void;
}) {
  return (
    <section>
      <header className="mb-2 flex items-center gap-2">
        <Image
          src={icon}
          alt=""
          width={20}
          height={20}
          unoptimized
          className="size-5"
        />
        <h3 className="text-sm font-semibold text-muted">
          {label}{" "}
          <span className="font-normal text-faint">({side})</span>
        </h3>
      </header>
      <ul className="divide-y divide-border overflow-hidden rounded-xl bg-card">
        {players.map((player) => (
          <PlayerRow
            key={player.id}
            player={player}
            matchId={matchId}
            onFlagToggled={onFlagToggled}
            onOpenPlayer={onOpenPlayer}
          />
        ))}
      </ul>
    </section>
  );
}

function PlayerRow({
  player,
  matchId,
  onFlagToggled,
  onOpenPlayer,
}: {
  player: MatchPlayerRow;
  matchId: string;
  onFlagToggled: (player: MatchPlayerRow, flagged: boolean, reason?: number, note?: string) => void;
  onOpenPlayer: (steam64Id: string) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [showFlagMenu, setShowFlagMenu] = useState(false);
  const flagMenuRef = useRef<HTMLDivElement>(null);
  const reasonText =
    player.suspected && player.reasons.length > 0
      ? `Suspicious: ${player.reasons.map((r) => `${r.name} (${r.detail})`).join(", ")}`
      : undefined;

  useEffect(() => {
    if (!showFlagMenu) return;
    const dismiss = (e: MouseEvent | KeyboardEvent) => {
      if (e instanceof KeyboardEvent && e.key === "Escape") {
        setShowFlagMenu(false);
        return;
      }
      if (e instanceof MouseEvent && flagMenuRef.current && !flagMenuRef.current.contains(e.target as Node)) {
        setShowFlagMenu(false);
      }
    };
    document.addEventListener("mousedown", dismiss);
    document.addEventListener("keydown", dismiss);
    return () => {
      document.removeEventListener("mousedown", dismiss);
      document.removeEventListener("keydown", dismiss);
    };
  }, [showFlagMenu]);

  const toggleFlag = async (reason?: number, note?: string) => {
    setBusy(true);
    setShowFlagMenu(false);
    onFlagToggled(player, !player.flagged, reason, note);
    try {
      await setPlayerFlag(matchId, player.id, !player.flagged, reason, note);
    } catch {
      onFlagToggled(player, player.flagged);
    } finally {
      setBusy(false);
    }
  };

  const flagTier = FLAG_REASONS.find((f) => f.value === player.flagReason);

  return (
    <li className="grid grid-cols-[1fr_auto_auto_auto] items-center gap-3 px-4 py-2 text-sm">
      <span className="flex min-w-0 items-center gap-2">
        <button
          type="button"
          onClick={() => onOpenPlayer(player.steam64Id)}
          className="truncate font-medium transition-colors hover:text-primary-light"
        >
          {player.name}
        </button>
        {player.suspected && (
          <span
            role="img"
            aria-label="Auto-flagged as suspicious"
            title={reasonText ?? "Auto-flagged as suspicious"}
            className="shrink-0 text-amber-400"
          >
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="size-3"
            >
              <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
              <path d="M12 9v4" />
              <path d="M12 17h.01" />
            </svg>
          </span>
        )}
      </span>
      <RankBadge rank={player.rank} />
      <span className="w-20 text-right font-mono text-xs text-muted">
        <span className="font-semibold text-foreground">{player.kills}</span>/
        {player.deaths}/{player.assists}
      </span>
      <div className="relative">
        <button
          type="button"
          disabled={busy}
          role="img"
          aria-label={
            player.flagged ? "Remove manual flag" : "Manually flag this player"
          }
          title={player.flagged ? "Remove manual flag" : "Manually flag this player"}
          onClick={() => {
            if (player.flagged) {
              toggleFlag(0, undefined);
            } else {
              setShowFlagMenu((v) => !v);
            }
          }}
          className={`transition-colors ${
            player.flagged
              ? flagTier?.color ?? "text-danger"
              : "text-faint hover:text-danger"
          } ${busy ? "opacity-50" : ""}`}
        >
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            className="size-4"
          >
            <path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z" />
            <line x1="4" x2="4" y1="22" y2="15" />
          </svg>
        </button>
        {showFlagMenu && (
          <div ref={flagMenuRef} className="absolute right-0 top-full z-50 mt-1 w-40 overflow-hidden rounded-xl border border-border bg-surface shadow-xl shadow-deep">
            {FLAG_REASONS.filter((f) => f.value !== 0).map((f) => (
              <button
                key={f.value}
                type="button"
                onClick={() => toggleFlag(f.value)}
                className={`block w-full px-4 py-2 text-left text-sm transition-colors hover:bg-hover ${f.color}`}
              >
                {f.label}
              </button>
            ))}
          </div>
        )}
      </div>
    </li>
  );
}

export function MatchDetailsModal({
  match,
  open,
  onClose,
}: {
  match: Match | null;
  open: boolean;
  onClose: () => void;
}) {
  const [roster, setRoster] = useState<MatchRoster | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [playerSteam64, setPlayerSteam64] = useState<string | null>(null);
  const loading =
    open && match?.status === "Parsed" && roster === null && error === null;

  useEffect(() => {
    if (!open || !match) return;
    if (match.status !== "Parsed") return;
    const controller = new AbortController();
    getMatchPlayers(match.id)
      .then((result) => {
        if (!controller.signal.aborted) setRoster(result);
      })
      .catch(() => {
        if (!controller.signal.aborted)
          setError("Could not load match details.");
      });
    return () => controller.abort();
  }, [open, match]);

  const handleFlagToggled = (player: MatchPlayerRow, flagged: boolean, reason?: number, note?: string) => {
    setRoster((current) =>
      current
        ? {
            ct: current.ct.map((p) =>
              p.id === player.id ? { ...p, flagged, flagReason: reason ?? p.flagReason, flagNote: note ?? p.flagNote } : p,
            ),
            t: current.t.map((p) =>
              p.id === player.id ? { ...p, flagged, flagReason: reason ?? p.flagReason, flagNote: note ?? p.flagNote } : p,
            ),
          }
        : current,
    );
  };

  const title = match ? `${match.map} · ${match.mode} · ${match.score}` : "";

  return (
    <>
      <Modal open={open} onClose={onClose} title={title} wide>
        {loading && (
          <p className="py-8 text-center text-sm text-muted">Loading roster…</p>
        )}
        {!loading && error && (
          <p className="py-8 text-center text-sm text-danger">{error}</p>
        )}
        {!loading && match && match.status !== "Parsed" && (
          <p className="py-8 text-center text-sm text-muted">
            {match.status === "Failed"
              ? "This demo could not be parsed, so no match details are available."
              : "This demo is still processing — details will appear once parsing completes."}
          </p>
        )}
        {!loading && !error && roster && (
          <div className="space-y-5">
            <TeamSection
              side="CT"
              icon="/ranks/teams/ct.svg"
              label="Counter-Terrorists"
              players={roster.ct}
              matchId={match!.id}
              onFlagToggled={handleFlagToggled}
              onOpenPlayer={setPlayerSteam64}
            />
            <TeamSection
              side="T"
              icon="/ranks/teams/t.svg"
              label="Terrorists"
              players={roster.t}
              matchId={match!.id}
              onFlagToggled={handleFlagToggled}
              onOpenPlayer={setPlayerSteam64}
            />
          </div>
        )}
      </Modal>

      <PlayerDetailModal
        key={playerSteam64 ?? "none"}
        steam64Id={playerSteam64}
        open={playerSteam64 !== null}
        onClose={() => setPlayerSteam64(null)}
      />
    </>
  );
}
