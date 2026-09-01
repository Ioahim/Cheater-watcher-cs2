# Cheater Watcher CS2

A tool for tracking and reporting suspected cheaters in Counter-Strike 2 matches.

## Stack

| Layer    | Technology                                                        |
| -------- | ----------------------------------------------------------------- |
| Frontend | Next.js (App Router) + TypeScript + React + Tailwind CSS (bun)    |
| API      | ASP.NET Core Web API (C#) with JWT authentication                 |
| Data     | Entity Framework Core + PostgreSQL                                |

## Structure

```
Cheater-watcher-cs2/
├── frontend/   # Next.js app (src/ directory, App Router)
└── backend/    # ASP.NET Core solution
    └── src/CheaterWatcher.Api/
```

## Prerequisites

- [bun](https://bun.sh) >= 1.3
- [.NET SDK](https://dotnet.microsoft.com) >= 10.0
- PostgreSQL running locally (or update the connection string)

## Getting started

### Backend

```bash
cd backend/src/CheaterWatcher.Api
dotnet run
```

- Swagger/OpenAPI docs are available in development mode.
- Update `ConnectionStrings:DefaultConnection` in `appsettings.json` to point to your PostgreSQL instance.
- Replace the placeholder `Jwt:SecretKey` before deploying. For local development prefer `dotnet user-secrets`.

### Frontend

```bash
cd frontend
bun install
bun dev
```

Open http://localhost:3000 in your browser.

## Demo ingestion

Matches reach the app in two ways:

1. **Manual upload** - drop any `.dem` file (max 500 MB) through the dashboard. It is parsed in the background and assigned to the selected account.
2. **Automatic share-code polling** - for accounts with a Steam ID + Game Authentication Code, the API polls Valve for new matches, downloads the demo from `replay*.valve.net`, decompresses it (BZip2) and parses it.

### Steam Web API key

Share-code polling requires **one** Steam Web API key per deployment
(get one at <https://steamcommunity.com/dev/apikey>). The key authenticates the
caller, not the player - a single key can poll any number of tracked players.
Each tracked player contributes their own **Game Authentication Code**
(CS2 → Settings → Game → "Enable Steam Cloud" / match auth code), stored on
their account record.

Never commit the key. Set it with user secrets:

```bash
cd backend/src/CheaterWatcher.Api
dotnet user-secrets set "Steam:WebApiKey" "<your-key>"
```

Anyone cloning this repo runs their own instance with their own key.

## Suspicion scoring & attribution

- `suspected` is computed by this app's rule-based scorer from aggregate stats
  of the [Leetify](https://leetify.com) public CS2 API (pre-aim, reaction time,
  headshot/spray accuracy, counter-strafing) plus platform-ban status.
- `flagged` is always a manual action by you.

