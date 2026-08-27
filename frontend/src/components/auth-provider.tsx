"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import {
  AuthError,
  clearToken,
  exchangeSteamCode,
  getMe,
  type AuthUser,
} from "@/lib/api";

interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  setUser: (user: AuthUser | null) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUserState] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const hash = window.location.hash;
    const steamMatch = hash.match(/#steam_code=([^&]+)/);
    if (steamMatch) {
      window.history.replaceState(null, "", window.location.pathname + window.location.search);
      exchangeSteamCode(steamMatch[1])
        .then((u) => setUserState(u))
        .catch(() => {})
        .finally(() => setLoading(false));
      return;
    }

    const steamError = hash.match(/#steam=(expired|failed)/);
    if (steamError) {
      window.history.replaceState(null, "", window.location.pathname + window.location.search);
    }

    getMe()
      .then((u) => setUserState(u))
      .catch((e: unknown) => {
        if (e instanceof AuthError) clearToken();
      })
      .finally(() => setLoading(false));
  }, []);

  const setUser = useCallback((value: AuthUser | null) => setUserState(value), []);

  const logout = useCallback(() => {
    clearToken();
    setUserState(null);
  }, []);

  return (
    <AuthContext.Provider value={{ user, loading, setUser, logout }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }
  return context;
}
