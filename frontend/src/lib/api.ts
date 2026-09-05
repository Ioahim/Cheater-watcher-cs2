import type {
  Account,
  AccountStats,
  Match,
  MatchRoster,
  PendingReplay,
  PlayerDetail,
  ReplaySettings,
  SaveReplayPathResult,
} from "./types";

// When NEXT_PUBLIC_API_URL is unset, the browser talks to the Next.js server same-origin
// and a Proxy forwards /api/* to the backend (see src/proxy.ts). This keeps the backend
// URL runtime-configurable for Docker instead of baking it at build time.
const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "";

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const headers: Record<string, string> = {
    ...Object.fromEntries(
      init?.headers instanceof Headers
        ? init.headers.entries()
        : init?.headers
          ? Object.entries(init.headers as Record<string, string>)
          : [],
    ),
  };
  if (
    !("Content-Type" in headers) &&
    !(init?.body instanceof FormData) &&
    init?.method &&
    ["POST", "PUT", "PATCH"].includes(init.method)
  ) {
    headers["Content-Type"] = "application/json";
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      ...init,
      headers,
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "Could not reach the server.");
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const msg =
      (body as { error?: string } | null)?.error ??
      `API error ${response.status} on ${path}`;
    throw new ApiError(response.status, msg);
  }

  if (response.status === 204) return undefined as T;

  return (await response.json()) as T;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

// --- Accounts ---

export async function getAccounts(): Promise<Account[]> {
  return request<Account[]>("/api/accounts");
}

export async function getAccountMatches(accountId: number): Promise<Match[]> {
  return request<Match[]>(`/api/accounts/${accountId}/matches`);
}

export async function getAccountStats(accountId: number): Promise<AccountStats> {
  return request<AccountStats>(`/api/accounts/${accountId}/stats`);
}

export async function getAccountsSummary(): Promise<AccountStats> {
  return request<AccountStats>("/api/accounts/summary");
}

// --- Players ---

export async function getPlayerDetail(steam64Id: string): Promise<PlayerDetail> {
  return request<PlayerDetail>(`/api/players/${steam64Id}/detail`);
}

export function steamReportUrl(steam64Id: string): string {
  return `https://help.steampowered.com/en/report_user/${steam64Id}`;
}

export function externalReportUrl(steam64Id: string): string {
  return `https://steamreport.net/report/${steam64Id}`;
}

// --- Upload ---

export interface UploadResult {
  matchId: string;
  duplicate: boolean;
}

export async function uploadDemo(
  accountId: number,
  file: File,
): Promise<UploadResult> {
  const form = new FormData();
  form.append("file", file);
  form.append("accountId", String(accountId));

  let response: Response;
  try {
    response = await fetch(`${API_BASE}/api/matches/upload`, {
      method: "POST",
      body: form,
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "Could not reach the server.");
  }

  if (!response.ok && response.status !== 202) {
    const body = await response.json().catch(() => null);
    const msg =
      (body as { error?: string } | null)?.error ??
      `Upload failed with ${response.status}`;
    throw new ApiError(response.status, msg);
  }
  return (await response.json()) as UploadResult;
}

// --- Matches ---

export interface MatchStatus {
  id: string;
  status: "Pending" | "Parsed" | "Failed";
  error: string | null;
  suspected: boolean;
  flagged: boolean;
}

export async function getMatchStatus(matchId: string): Promise<MatchStatus> {
  return request<MatchStatus>(`/api/matches/${matchId}`);
}

export async function getMatchPlayers(matchId: string): Promise<MatchRoster> {
  return request<MatchRoster>(`/api/matches/${matchId}/players`);
}

export async function setPlayerFlag(
  matchId: string,
  playerId: number,
  flagged: boolean,
  reason?: number,
  note?: string,
): Promise<void> {
  await request<unknown>(
    `/api/matches/${matchId}/players/${playerId}/flag`,
    {
      method: flagged ? "POST" : "DELETE",
      headers: flagged ? { "Content-Type": "application/json" } : undefined,
      body: flagged ? JSON.stringify({ reason: reason ?? 1, note }) : undefined,
    },
  );
}

export async function setMatchFlag(
  matchId: string,
  flagged: boolean,
): Promise<void> {
  await request<unknown>(
    `/api/matches/${matchId}/flag`,
    { method: flagged ? "POST" : "DELETE" },
  );
}

// --- Steam linking (anonymous) ---

export async function getSteamLinkUrl(): Promise<string> {
  const res = await request<{ url: string }>("/api/accounts/steam/link");
  return res.url;
}

export async function exchangeSteamCode(code: string): Promise<void> {
  await request<unknown>("/api/accounts/steam/exchange", {
    method: "POST",
    body: JSON.stringify({ code }),
  });
}

export async function unlinkAccount(accountId: number): Promise<void> {
  await request(`/api/accounts/${accountId}`, { method: "DELETE" });
}

export async function reorderAccounts(ids: number[]): Promise<void> {
  await request("/api/accounts/reorder", {
    method: "POST",
    body: JSON.stringify({ order: ids }),
  });
}

// --- Replay scanning ---

export async function getReplaySettings(): Promise<ReplaySettings> {
  return request<ReplaySettings>("/api/replays/settings");
}

export async function updateReplaySettings(path: string): Promise<SaveReplayPathResult> {
  return request<SaveReplayPathResult>("/api/replays/settings", {
    method: "PUT",
    body: JSON.stringify({ path }),
  });
}

export async function scanReplays(): Promise<void> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE}/api/replays/scan`, {
      method: "POST",
      cache: "no-store",
    });
  } catch {
    throw new ApiError(0, "Could not reach the server.");
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new ApiError(
      response.status,
      (body as { error?: string } | null)?.error ?? "Could not trigger a scan.",
    );
  }
}

export async function getPendingReplays(): Promise<PendingReplay[]> {
  return request<PendingReplay[]>("/api/replays/pending");
}

export async function resolvePendingReplay(
  id: string,
  accountId: number,
): Promise<void> {
  await request(`/api/replays/pending/${id}/resolve`, {
    method: "POST",
    body: JSON.stringify({ accountId }),
  });
}

export async function dismissPendingReplay(id: string): Promise<void> {
  await request(`/api/replays/pending/${id}/resolve`, {
    method: "POST",
    body: JSON.stringify({ dismiss: true }),
  });
}
