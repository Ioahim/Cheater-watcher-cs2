# Cheater Watcher CS2

Tracks, scores, and flags suspected cheaters in your Counter-Strike 2 demos

## Stack

| Layer    | Technology |
| -------- | ---------- |
| Frontend | Next.js (App Router) + TypeScript + React + Tailwind (bun) |
| API      | ASP.NET Core Web API (C#) |
| Data     | Entity Framework Core + PostgreSQL (Docker) |

## Quick start (Docker)

1. `./start.ps1` on Windows, or `./start.sh` on macOS/Linux. On first run the script
   creates `.env` from `.env.example` (with a random `POSTGRES_PASSWORD`) and starts
   the stack.
2. Open http://localhost:3000. EF Core auto-applies migrations on backend start.
3. First run walkthrough: link a Steam account on **Accounts**, set your replays folder
   from **Accounts** → Account settings → **Replays folder** (saving it writes `.env`; run
   `docker compose up -d` once, then reload), and set a **Steam API key** on **Accounts**
   (saving it writes `.env`; run `docker compose up -d` once, then reload).

## Pages

- **Matches** - upload a `.dem` (assigned to the active account, deleted after parse),
  and the replay-folder scanner panel (scan now, attribute pending demos).
- **Stats** - per-account tabs plus an **All accounts** summary; flagged-players list and VAC ban counter.
- **Accounts** - link/reorder/remove Steam accounts (OpenID), see each account's ranks, and
  configure the Steam API key and the replays folder under **Account settings**.

## How demos become matches

- **Replay folder scanner** - watches your CS2 replays folder (read-only bind mount,
  configured in-app on the **Accounts** page). No background downloader touches your Steam account.
- **Manual upload** - upload any `.dem`; shown with no date (uploads are for recordings/old games).

## Steam Web API key (recommended)

Set it from the **Accounts** page ("Steam API key") or by editing `STEAM_WEB_API_KEY`
in `.env` directly. Either way, run `docker compose up -d` once so the backend picks
it up. It enables:
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