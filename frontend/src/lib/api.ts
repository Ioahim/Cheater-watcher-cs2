import type {
  Account,
  AccountStats,
  AuthResponse,
  AuthUser,
  Match,
  MatchRoster,
  PlayerDetail,
} from "./types";

export type { AuthResponse, AuthUser };

const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5089";
const TOKEN_KEY = "cw_token";

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string): void {
  localStorage.setItem(TOKEN_KEY, token);
}

export function clearToken(): void {
  localStorage.removeItem(TOKEN_KEY);
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = getToken();
  const headers: Record<string, string> = {
    ...Object.fromEntries(
      init?.headers instanceof Headers
        ? init.headers.entries()
        : init?.headers
          ? Object.entries(init.headers as Record<string, string>)
          : [],
    ),
  };
  if (token) {
    headers["Authorization"] = `Bearer ${token}`;
  }
  if (
    !("Content-Type" in headers) &&
    !(init?.body instanceof FormData) &&
    init?.method &&
    ["POST", "PUT", "PATCH"].includes(init.method)
  ) {
    headers["Content-Type"] = "application/json";
  }

  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers,
    cache: "no-store",
  });

  if (response.status === 401) {
    clearToken();
    throw new AuthError();
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null);
    const msg =
      (body as { error?: string } | null)?.error ??
      `API error ${response.status} on ${path}`;
    throw new ApiError(response.status, msg);
  }

  return (await response.json()) as T;
}

export class AuthError extends Error {
  constructor() {
    super("Unauthorized");
    this.name = "AuthError";
  }
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

  const token = getToken();
  const headers: Record<string, string> = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE}/api/matches/upload`, {
    method: "POST",
    headers,
    body: form,
  });
  if (!response.ok && response.status !== 202) {
    throw new Error(`Upload failed with ${response.status}`);
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
  const token = getToken();
  const headers: Record<string, string> = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;
  if (flagged) headers["Content-Type"] = "application/json";

  const response = await fetch(
    `${API_BASE}/api/matches/${matchId}/players/${playerId}/flag`,
    {
      method: flagged ? "POST" : "DELETE",
      headers,
      body: flagged ? JSON.stringify({ reason: reason ?? 1, note }) : undefined,
    },
  );
  if (!response.ok && response.status !== 204) {
    throw new Error(`Player flag update failed with ${response.status}`);
  }
}

export async function setMatchFlag(
  matchId: string,
  flagged: boolean,
): Promise<void> {
  const token = getToken();
  const headers: Record<string, string> = {};
  if (token) headers["Authorization"] = `Bearer ${token}`;

  const response = await fetch(`${API_BASE}/api/matches/${matchId}/flag`, {
    method: flagged ? "POST" : "DELETE",
    headers,
  });
  if (!response.ok && response.status !== 204) {
    throw new Error(`Flag update failed with ${response.status}`);
  }
}

// --- Auth ---

export async function register(
  username: string,
  password: string,
): Promise<AuthResponse> {
  return request<AuthResponse>("/api/auth/register", {
    method: "POST",
    body: JSON.stringify({ username, password }),
  });
}

export async function login(
  username: string,
  password: string,
): Promise<AuthResponse> {
  return request<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ username, password }),
  });
}

export async function getMe(): Promise<AuthUser> {
  return request<AuthUser>("/api/auth/me");
}

export async function getSteamLinkUrl(): Promise<string> {
  const res = await request<{ url: string }>("/api/auth/steam/link", {
    method: "GET",
  });
  return res.url;
}

export async function exchangeSteamCode(
  code: string,
): Promise<AuthUser> {
  return request<AuthUser>("/api/auth/steam/exchange", {
    method: "POST",
    body: JSON.stringify({ code }),
  });
}

export async function updateCredentials(
  accountId: number,
  steam64Id: string | null,
  authCode: string | null,
): Promise<void> {
  await request(`/api/accounts/${accountId}/credentials`, {
    method: "PATCH",
    body: JSON.stringify({ steam64Id, authCode }),
  });
}

export const API_BASE_URL = API_BASE;
