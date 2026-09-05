# Cheater Watcher CS2

Tracks, scores, and flags suspected cheaters in your Counter-Strike 2 demos

## Stack

| Layer    | Technology |
| -------- | ---------- |
| Frontend | Next.js (App Router) + TypeScript + React + Tailwind (bun) |
| API      | ASP.NET Core Web API (C#) |
| Data     | Entity Framework Core + PostgreSQL (Docker) |

## Quick start (Docker)

1. `cp .env.example .env` and fill in at least `POSTGRES_PASSWORD`.
2. `docker compose up -d --build`
3. Open http://localhost:3000. EF Core auto-applies migrations on backend start.

## Pages

- **Matches** - upload a `.dem` (assigned to the active account, deleted after parse),
  and the replay-folder scanner panel (set the folder, scan now, attribute pending demos).
- **Stats** - per-account tabs plus an **All accounts** summary; flagged-players list and VAC ban counter.
- **Accounts** - link/reorder/remove Steam accounts (OpenID), see each account's ranks.

## How demos become matches

- **Replay folder scanner** - watches your CS2 replays folder (read-only bind mount,
  configured in-app). No background downloader touches your Steam account.
- **Manual upload** - upload any `.dem`; shown with no date (uploads are for recordings/old games).

## Steam Web API key (recommended)

Set `STEAM_WEB_API_KEY` in `.env` to enable:
- steam account name/avatar enrichment for linked accounts, and
- **VAC ban checks** - when you manually flag a player as Cheating/Suspicious the app
  checks Steam; VAC-banned players get a badge, add to the banned counter, and show in
  the stats flagged list. Without the key these are skipped.

Get a key at https://steamcommunity.com/dev/apikey

## Suspicion scoring

A rule-based scorer composed of the configurable `Suspicion` values in
`appsettings.json`, fed by Leetify's public per-player stats (pre-aim, reaction time,
headshot/spray accuracy, counter-strafing). Using donk as a benchmark, players crossing the
weighted threshold (default `Threshold=45`) show as **Suspicious**. Manual flags
(Cheating/Griefing/Toxic/Suspicious) are separate.