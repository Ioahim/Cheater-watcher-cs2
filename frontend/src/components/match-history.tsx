import type { Match, MatchStatus } from "@/lib/types";
import { formatDate } from "@/lib/format";
import { RankBadge } from "./rank-badge";

function StatusBadge({ status }: { status: MatchStatus }) {
  if (status === "Parsed") return null;
  const pending = status === "Pending";
  return (
    <span
      className={`shrink-0 rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${
        pending
          ? "bg-amber-400/15 text-amber-400"
          : "bg-danger/15 text-danger"
      }`}
    >
      {pending ? "Processing…" : "Failed"}
    </span>
  );
}

function ResultBadge({ result }: { result: Match["result"] }) {
  const win = result === "W";
  const draw = result === "D";
  return (
    <span
      className={`flex size-8 shrink-0 items-center justify-center rounded-lg text-sm font-bold ${
        win
          ? "bg-success/15 text-success"
          : draw
            ? "bg-muted/15 text-muted"
            : "bg-danger/15 text-danger"
      }`}
    >
      {result}
    </span>
  );
}

export function MatchHistory({
  matches,
  onOpenDetails,
}: {
  matches: Match[];
  onOpenDetails?: (match: Match) => void;
}) {
  return (
    <ul className="divide-y divide-border">
      {matches.map((match) => (
        <li
          key={match.id}
          onClick={onOpenDetails ? () => onOpenDetails(match) : undefined}
          className={`relative grid grid-cols-[auto_3.5rem_1fr_auto_auto] items-center gap-4 px-5 py-3 transition-colors hover:bg-hover/50 ${
            onOpenDetails ? "cursor-pointer" : ""
          }`}
        >
          <ResultBadge result={match.result} />
          <span className="font-mono text-sm font-semibold">
            {match.score}
          </span>
          <span className="truncate pr-20 text-sm font-medium">
            {match.map}
          </span>
          <span className="flex w-9 items-center justify-end gap-2">
            {match.status === "Parsed" && !match.scoredAt ? (
              <span
                role="status"
                aria-label="Checking suspicious players"
                title="Checking suspicious players"
                className="text-faint"
              >
                <svg
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  className="size-4 animate-spin"
                  aria-hidden="true"
                >
                  <path d="M21 12a9 9 0 1 1-6.219-8.56" />
                </svg>
              </span>
            ) : match.suspected ? (
              <span
                role="img"
                aria-label="Suspicious player detected in this match"
                title="Suspicious player detected in this match"
                className="text-amber-400"
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
                  <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
                  <path d="M12 9v4" />
                  <path d="M12 17h.01" />
                </svg>
              </span>
            ) : null}
            {(match.hasFlaggedPlayer || match.flagged) && (
              <span
                role="img"
                aria-label="Flagged player in this match"
                title="Flagged player in this match"
                className="text-danger"
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
              </span>
            )}
          </span>
          <span className="text-right text-xs text-faint">{match.date ? formatDate(match.date) : "—"}</span>
          <span className="absolute left-1/2 top-1/2 hidden w-44 translate-x-[calc(-50%_-_4rem)] -translate-y-1/2 items-center gap-2 text-sm text-muted sm:flex">
            <span className="w-24 shrink-0">{match.mode}</span>
            <StatusBadge status={match.status} />
            <RankBadge rank={match.rank} />
          </span>
        </li>
      ))}
    </ul>
  );
}
