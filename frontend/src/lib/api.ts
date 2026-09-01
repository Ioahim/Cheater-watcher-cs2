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

// When NEXT_PUBLIC_API_URL is unset, the browser talks to the Next.js server same-origin
// and a Proxy forwards /api/* to the backend (see src/proxy.ts). This keeps the backend
// URL runtime-configurable for Docker instead of baking it at build time.
const API_BASE = process.env.NEXT_PUBLIC_API_URL ?? "";
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

interface RequestOptions {
  authExpected401?: boolean;
}

async function request<T>(
  path: string,
  init?: RequestInit,
  opts?: RequestOptions,
): Promise<T> {
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

  if (response.status === 401 && !opts?.authExpected401) {
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

  if (response.status === 204) return undefined as T;

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
  if (response.status === 401) {
    clearToken();
    throw new AuthError();
  }
  if (!response.ok && response.status !== 202) {
    throw new Error(`Upload failed with ${response.status}`);
  }
  return (await response.json()) as UploadResult;
}

export interface AddShareCodeResult {
  status: "invalid" | "duplicate" | "download_failed" | "ingested";
  matchId?: string | null;
}

export async function addShareCode(
  accountId: number,
  shareCode: string,
): Promise<AddShareCodeResult> {
  return request<AddShareCodeResult>("/api/matches/share", {
    method: "POST",
    body: JSON.stringify({ accountId, shareCode }),
  });
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
  if (response.status === 401) {
    clearToken();
    throw new AuthError();
  }
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
  if (response.status === 401) {
    clearToken();
    throw new AuthError();
  }
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
  }, { authExpected401: true });
}

export async function login(
  username: string,
  password: string,
): Promise<AuthResponse> {
  return request<AuthResponse>("/api/auth/login", {
    method: "POST",
    body: JSON.stringify({ username, password }),
  }, { authExpected401: true });
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

export interface CredentialSaveResult {
  status: "invalid" | "duplicate" | "download_failed" | "ingested";
  matchId?: string | null;
}

export async function updateCredentials(
  accountId: number,
  steam64Id: string | null,
  authCode: string | null,
  shareCode?: string | null,
): Promise<CredentialSaveResult | null> {
  return request<CredentialSaveResult | null>(
    `/api/accounts/${accountId}/credentials`,
    {
      method: "PATCH",
      body: JSON.stringify({ steam64Id, authCode, shareCode }),
    },
  );
}

export async function unlinkAccount(accountId: number): Promise<void> {
  await request(`/api/accounts/${accountId}`, { method: "DELETE" });
}
