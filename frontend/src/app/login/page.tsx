"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { useAuth } from "@/components/auth-provider";
import { login, register, setToken } from "@/lib/api";

export default function LoginPage() {
  const { user, setUser } = useAuth();
  const router = useRouter();
  const [tab, setTab] = useState<"login" | "register">("login");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (user) router.replace("/");
  }, [user, router]);

  const handleSubmit = useCallback(
    async (event: FormEvent) => {
      event.preventDefault();
      setError("");

      const name = username.trim();
      if (tab === "register") {
        if (name.length < 3 || name.length > 32 || !/^[A-Za-z0-9_-]+$/.test(name)) {
          setError("Username must be 3-32 characters (letters, digits, _ or -).");
          return;
        }
        if (password.length < 8 || password.length > 128) {
          setError("Password must be 8-128 characters.");
          return;
        }
      }

      setSubmitting(true);
      try {
        const resp =
          (await (tab === "login" ? login : register)(name, password));
        setToken(resp.token);
        setUser(resp.user);
      } catch (e) {
        setError(e instanceof Error ? e.message : "Something went wrong.");
      } finally {
        setSubmitting(false);
      }
    },
    [tab, username, password, setUser],
  );

  if (user) return null;

  return (
    <div className="flex flex-1 items-center justify-center px-4 py-16">
      <div className="w-full max-w-md space-y-6">
        <div className="text-center">
          <h1 className="text-xl font-semibold">Cheater Watcher</h1>
          <p className="mt-2 text-sm text-muted">
            {tab === "login"
              ? "Sign in to your account"
              : "Create a new account"}
          </p>
        </div>

        <div className="flex rounded-lg border border-border bg-surface p-1">
          {(["login", "register"] as const).map((t) => (
            <button
              key={t}
              type="button"
              onClick={() => {
                setTab(t);
                setError("");
              }}
              className={`flex-1 rounded-lg px-3 py-2 text-sm font-medium transition-colors ${
                tab === t
                  ? "bg-card text-foreground"
                  : "text-muted hover:text-foreground"
              }`}
            >
              {t === "login" ? "Sign in" : "Create account"}
            </button>
          ))}
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1">
            <label htmlFor="username" className="text-sm font-medium text-muted">
              Username
            </label>
            <input
              id="username"
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              autoFocus
              autoComplete="username"
              className="w-full rounded-lg border border-border bg-card px-4 py-2 text-sm placeholder:text-faint focus:border-primary focus:outline-none"
              placeholder="Your username"
            />
          </div>

          <div className="space-y-1">
            <label htmlFor="password" className="text-sm font-medium text-muted">
              Password
            </label>
            <input
              id="password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete={tab === "login" ? "current-password" : "new-password"}
              className="w-full rounded-lg border border-border bg-card px-4 py-2 text-sm placeholder:text-faint focus:border-primary focus:outline-none"
              placeholder={tab === "register" ? "8+ characters" : "Your password"}
            />
          </div>

          {error && (
            <p className="rounded-lg border border-danger/40 bg-danger/10 px-4 py-2 text-sm text-danger">
              {error}
            </p>
          )}

          <button
            type="submit"
            disabled={!username.trim() || !password.trim() || submitting}
            className="w-full rounded-lg bg-primary px-4 py-2 text-sm font-semibold text-white transition-colors hover:bg-primary-light disabled:cursor-not-allowed disabled:opacity-40"
          >
            {submitting
              ? "Please wait..."
              : tab === "login"
                ? "Sign in"
                : "Create account"}
          </button>
        </form>
      </div>
    </div>
  );
}
