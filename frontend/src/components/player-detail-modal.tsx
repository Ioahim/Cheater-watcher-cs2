"use client";

import { useEffect, useRef, useState } from "react";
import {
  externalReportUrl,
  getPlayerDetail,
  steamReportUrl,
} from "@/lib/api";
import type { PlayerDetail } from "@/lib/types";
import { FLAG_REASONS } from "@/lib/types";
import { Modal } from "./modal";

function FlagLabel({ reason }: { reason: number }) {
  const tier = FLAG_REASONS.find((f) => f.value === reason);
  if (!tier || reason === 0) return null;
  return (
    <span className={`text-xs font-medium ${tier.color}`}>
      {tier.label}
    </span>
  );
}

export function PlayerDetailModal({
  steam64Id,
  open,
  onClose,
}: {
  steam64Id: string | null;
  open: boolean;
  onClose: () => void;
}) {
  const [detail, setDetail] = useState<PlayerDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const fetchIdRef = useRef(0);
  const loading = open && detail === null && error === null;

  useEffect(() => {
    if (!open || !steam64Id) return;
    const id = ++fetchIdRef.current;
    getPlayerDetail(steam64Id)
      .then((data) => {
        if (fetchIdRef.current === id) setDetail(data);
      })
      .catch(() => {
        if (fetchIdRef.current === id) setError("Could not load player details.");
      });
  }, [open, steam64Id]);

  const title = detail?.name ?? "Player";

  return (
    <Modal open={open} onClose={onClose} title={title} wide>
      {loading && (
        <p className="py-8 text-center text-sm text-muted">Loading player data…</p>
      )}
      {!loading && error && (
        <p className="py-8 text-center text-sm text-danger">{error}</p>
      )}
      {!loading && !error && detail && (
        <div className="space-y-5">
          {detail.steam64Id && (
            <p className="text-xs text-faint break-all">
              Steam64: {detail.steam64Id}
            </p>
          )}

          <div className="grid grid-cols-3 gap-4">
            <StatBlock
              label="Encounters"
              value={detail.timesEncountered}
            />
            <StatBlock
              label="On our team"
              value={detail.timesOnOurTeam}
            />
            <StatBlock
              label="Against us"
              value={detail.timesAgainstUs}
            />
          </div>

          <div className="grid grid-cols-3 gap-4">
            <StatBlock
              label="K/D ratio"
              value={
                detail.totalDeaths > 0
                  ? (detail.totalKills / detail.totalDeaths).toFixed(2)
                  : detail.totalKills.toFixed(2)
              }
            />
            <StatBlock label="Total kills" value={detail.totalKills} />
            <StatBlock label="Total deaths" value={detail.totalDeaths} />
          </div>

          {detail.flagged && (
            <div className="rounded-lg bg-danger/10 px-4 py-3">
              <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-danger">Flagged</span>
                <FlagLabel reason={detail.flagReason} />
              </div>
              {detail.flagNote && (
                <p className="mt-1 text-xs text-muted">{detail.flagNote}</p>
              )}
            </div>
          )}

          <div className="flex flex-wrap gap-2">
            {detail.steam64Id && (
              <a
                href={steamReportUrl(detail.steam64Id)}
                target="_blank"
                rel="noreferrer"
                className="rounded-lg border border-border px-4 py-2 text-sm text-muted transition-colors hover:bg-hover hover:text-foreground"
              >
                Report on Steam
              </a>
            )}
            {detail.steam64Id && (
              <a
                href={externalReportUrl(detail.steam64Id)}
                target="_blank"
                rel="noreferrer"
                className="rounded-lg border border-border px-4 py-2 text-sm text-muted transition-colors hover:bg-hover hover:text-foreground"
              >
                External report
                <span className="ml-1 text-xs text-faint">(ToS risk)</span>
              </a>
            )}
          </div>

          {detail.encounters.length > 0 && (
            <section className="space-y-2">
              <h3 className="text-sm font-semibold">Encounter history</h3>
              <div className="max-h-64 space-y-1 overflow-y-auto">
                {detail.encounters.map((e) => (
                  <div
                    key={e.matchId}
                    className="flex items-center justify-between rounded-lg bg-card px-3 py-2 text-xs"
                  >
                    <span className="flex items-center gap-3">
                      <span
                        className={`w-5 font-bold ${
                          e.result === "W"
                            ? "text-success"
                            : e.result === "L"
                              ? "text-danger"
                              : "text-muted"
                        }`}
                      >
                        {e.result}
                      </span>
                      <span className="text-muted">{e.map}</span>
                      <span className="text-faint">{e.mode}</span>
                    </span>
                    <span className="flex items-center gap-3">
                      <span className="font-mono text-muted">
                        <span className="font-semibold text-foreground">{e.kills}</span>/{e.deaths}/{e.assists}
                      </span>
                      <span className="text-faint">{e.date}</span>
                    </span>
                  </div>
                ))}
              </div>
            </section>
          )}
        </div>
      )}
    </Modal>
  );
}

function StatBlock({ label, value }: { label: string; value: number | string }) {
  return (
    <div className="rounded-lg bg-card px-3 py-3 text-center">
      <p className="text-xl font-bold">{value}</p>
      <p className="mt-1 text-xs text-muted">{label}</p>
    </div>
  );
}
