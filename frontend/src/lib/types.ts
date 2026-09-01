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
}

export interface MatchRoster {
  ct: MatchPlayerRow[];
  t: MatchPlayerRow[];
}

export type MatchStatus = "Pending" | "Parsed" | "Failed";

export interface Match {
  id: string;
  result: MatchResult;
  score: string;
  map: string;
  mode: string;
  rank: Rank | null;
  date: string;
  suspected?: boolean;
  flagged?: boolean;
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
  trackingEnabled?: boolean;
  needsShareCode?: boolean;
}

export interface AuthUser {
  id: number;
  username: string;
  steam64Id: string | null;
  avatarUrl: string | null;
  ownAccountId: number | null;
  personaName?: string | null;
}

export interface AuthResponse {
  token: string;
  user: AuthUser;
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
  winRate: number;
  totalPlayers: number;
  byMap: MapStat[];
  byMode: ModeStat[];
}

export interface PlayerEncounter {
  matchId: string;
  map: string;
  mode: string;
  date: string;
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
  encounters: PlayerEncounter[];
}
