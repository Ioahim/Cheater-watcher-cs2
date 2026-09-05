export type MatchResult = "W" | "L" | "D";

export type Rank =
  | { kind: "premier"; rating: number }
  | { kind: "competitive"; level: number }
  | { kind: "wingman"; level: number };

export interface MapRank {
  map: string;
  level: number;
}

export interface PlayerReason {
  name: string;
  detail: string;
}

export interface MatchPlayerRow {
  id: number;
  name: string;
  steam64Id: string;
  kills: number;
  deaths: number;
  assists: number;
  suspected: boolean;
  reasons: PlayerReason[];
  flagged: boolean;
  flagReason: number;
  flagNote: string | null;
  rank: Rank | null;
  vacBanned?: boolean;
  isOwnAccount?: boolean;
}

export interface MatchRoster {
  ct: MatchPlayerRow[];
  t: MatchPlayerRow[];
  averageRank?: Rank | null;
}

export type MatchStatus = "Pending" | "Parsed" | "Failed";

export interface Match {
  id: string;
  result: MatchResult;
  score: string;
  map: string;
  mode: string;
  rank: Rank | null;
  date: string | null;
  suspected?: boolean;
  scoredAt: string | null;
  flagged?: boolean;
  hasFlaggedPlayer?: boolean;
  status: MatchStatus;
}

export interface Account {
  id: number;
  name: string;
  avatarUrl?: string | null;
  premierRating?: number | null;
  wingmanLevel?: number | null;
  competitiveRanks: MapRank[];
  steamLinked?: boolean;
}

export const FLAG_REASONS = [
  { value: 0, label: "None", color: "" },
  { value: 1, label: "Cheating", color: "text-danger" },
  { value: 2, label: "Griefing", color: "text-amber-400" },
  { value: 3, label: "Toxic", color: "text-pink-400" },
  { value: 4, label: "Suspicious", color: "text-primary-light" },
] as const;

export interface MapStat {
  map: string;
  matches: number;
  winRate: number;
}

export interface ModeStat {
  mode: string;
  matches: number;
}

export interface AccountStats {
  totalMatches: number;
  flaggedMatches: number;
  flaggedPlayers: number;
  bannedPlayers: number;
  winRate: number;
  totalPlayers: number;
  byMap: MapStat[];
  byMode: ModeStat[];
  flaggedPlayersList: FlaggedPlayer[];
}

export interface FlaggedPlayer {
  steam64Id: string;
  name: string;
  flagReason: number;
  flagNote: string | null;
  vacBanned: boolean;
  encounters: number;
}

export interface PlayerEncounter {
  matchId: string;
  map: string;
  mode: string;
  date: string | null;
  result: MatchResult;
  kills: number;
  deaths: number;
  assists: number;
  teamNumber: number;
  flagReason: number;
  flagNote: string | null;
}

export interface PlayerDetail {
  steam64Id: string;
  name: string;
  timesEncountered: number;
  timesOnOurTeam: number;
  timesAgainstUs: number;
  totalKills: number;
  totalDeaths: number;
  totalAssists: number;
  flagged: boolean;
  flagReason: number;
  flagNote: string | null;
  vacBanned?: boolean;
  encounters: PlayerEncounter[];
}

// --- Replay scanning ---

export interface ReplaySettings {
  hasPath: boolean;
  hostPath: string;
  effectivePath: string;
  scanIntervalMinutes: number;
  restartRequired: boolean;
  lastScanAt: string | null;
  lastScanNew: number;
  lastScanAttributed: number;
  lastScanPending: number;
  lastScanError: string | null;
}

export interface SaveReplayPathResult {
  saved: boolean;
  restartRequired: boolean;
  canWriteEnv: boolean;
  hostPath: string;
}

// --- App settings ---

export interface SteamKeyStatus {
  configured: boolean;
  active: boolean;
  keyHint: string | null;
  restartRequired: boolean;
  canWriteEnv: boolean;
}

export interface SaveSteamKeyResult {
  saved: boolean;
  valid: boolean;
  checked: boolean;
  restartRequired: boolean;
  canWriteEnv: boolean;
}

export interface PendingReplayPlayer {
  steam64Id: string;
  name: string;
  linked: boolean;
}

export interface PendingReplay {
  id: string;
  fileName: string;
  mapName: string;
  mode: string;
  discoveredAt: string;
  players: PendingReplayPlayer[];
  linkedAccountOptions: number[];
}

