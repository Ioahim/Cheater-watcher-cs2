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
