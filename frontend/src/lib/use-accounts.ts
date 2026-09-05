"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { getAccounts } from "./api";
import type { Account } from "./types";

export function useAccounts() {
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [activeAccountId, setActiveAccountId] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const cancelledRef = useRef(false);

  const refresh = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const live = await getAccounts();
      if (cancelledRef.current) return;
      setAccounts(live);
      setActiveAccountId((prev) =>
        prev != null && live.some((a) => a.id === prev)
          ? prev
          : (live[0]?.id ?? null),
      );
    } catch {
      if (!cancelledRef.current) {
        setAccounts([]);
        setActiveAccountId(null);
        setError("Could not load accounts.");
      }
    } finally {
      if (!cancelledRef.current) setLoading(false);
    }
  }, []);

  useEffect(() => {
    cancelledRef.current = false;
    const run = async () => {
      await Promise.resolve();
      refresh();
    };
    void run();
    return () => { cancelledRef.current = true; };
  }, [refresh]);

  return { accounts, activeAccountId, setActiveAccountId, loading, error, refresh };
}
