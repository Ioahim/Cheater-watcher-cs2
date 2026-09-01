"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "./auth-provider";

const navLinks = [
  { href: "/stats", label: "Stats" },
  { href: "/matches", label: "Matches" },
  { href: "/settings", label: "Settings" },
];

function SteamIcon({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={className} aria-hidden>
      <path d="M12 2a10 10 0 0 0-9.97 9.29l5.35 2.21a2.82 2.82 0 0 1 1.6-.49h.14l2.38-3.44v-.05a3.76 3.76 0 1 1 3.76 3.75h-.09l-3.39 2.42v.11a2.83 2.83 0 0 1-5.63.34l-3.83-1.58A10 10 0 1 0 12 2Zm-4.4 13.62 1.23.51a2.12 2.12 0 1 0 1.15-2.77l1.27.53a1.56 1.56 0 1 1-1.2 2.88l-2.45-1.15Zm8.9-4.87a2.5 2.5 0 1 0-5 0 2.5 2.5 0 0 0 5 0Zm-4.37 0a1.88 1.88 0 1 1 3.76 0 1.88 1.88 0 0 1-3.76 0Z" />
    </svg>
  );
}

function UserAvatar({ name, avatarUrl }: { name: string; avatarUrl?: string | null }) {
  if (avatarUrl) {
    return (
      // eslint-disable-next-line @next/next/no-img-element -- remote Steam avatar, client-only
      <img
        src={avatarUrl}
        alt={name}
        className="size-8 shrink-0 rounded-full object-cover"
      />
    );
  }
  return (
    <span className="flex size-8 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary to-primary-light text-xs font-bold text-white">
      {name.charAt(0).toUpperCase()}
    </span>
  );
}

export function Navbar() {
  const pathname = usePathname();
  const { user, loading, logout } = useAuth();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!menuOpen) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") setMenuOpen(false);
    };
    const onClick = (event: MouseEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setMenuOpen(false);
    };
    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("mousedown", onClick);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("mousedown", onClick);
    };
  }, [menuOpen]);

  return (
    <>
      <header className="bg-surface">
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4">
          <Link href="/" className="flex items-center gap-2">
            <span className="flex size-8 items-center justify-center rounded-lg bg-primary font-mono text-sm font-bold text-white">
              CW
            </span>
            <span className="text-sm font-semibold tracking-wide">
              Cheater Watcher
            </span>
          </Link>

          <nav className="flex items-center gap-1">
            {navLinks.map((link) => {
              const active = pathname === link.href;
              return (
                <Link
                  key={link.href}
                  href={link.href}
                  className={`rounded-lg px-3 py-2 text-sm transition-colors ${
                    active
                      ? "bg-card font-medium text-foreground"
                      : "text-muted hover:bg-hover hover:text-foreground"
                  }`}
                >
                  {link.label}
                </Link>
              );
            })}
          </nav>

          <div className="ml-auto flex items-center gap-2">
            {!loading && (
              <>
                {user ? (
                  <div ref={menuRef} className="relative">
                    <button
                      type="button"
                      onClick={() => setMenuOpen((open) => !open)}
                      aria-haspopup="menu"
                      aria-expanded={menuOpen}
                      className="flex items-center gap-2 rounded-lg py-1 pl-1 pr-2 transition-colors hover:bg-hover"
                    >
                      <UserAvatar name={user.username} avatarUrl={user.avatarUrl} />
                      <span className="text-sm font-medium">{user.username}</span>
                      <svg
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        className={`size-3 text-muted transition-transform ${menuOpen ? "rotate-180" : ""}`}
                        aria-hidden
                      >
                        <path d="m6 9 6 6 6-6" />
                      </svg>
                    </button>

                    {menuOpen && (
                      <div
                        role="menu"
                        className="absolute right-0 top-full z-50 mt-2 w-44 overflow-hidden rounded-xl border border-border bg-surface shadow-xl shadow-deep"
                      >
                        <Link
                          href="/settings"
                          onClick={() => setMenuOpen(false)}
                          className="block w-full px-4 py-2 text-left text-sm text-muted transition-colors hover:bg-hover hover:text-foreground"
                        >
                          Settings
                        </Link>
                        <button
                          type="button"
                          role="menuitem"
                          onClick={() => {
                            setMenuOpen(false);
                            logout();
                          }}
                          className="block w-full px-4 py-2 text-left text-sm text-danger transition-colors hover:bg-danger/10"
                        >
                          Log out
                        </button>
                      </div>
                    )}
                  </div>
                ) : (
                  <Link
                    href="/login"
                    className="flex items-center gap-2 rounded-lg bg-primary px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-primary-light"
                  >
                    <SteamIcon className="size-4" />
                    Sign in
                  </Link>
                )}
              </>
            )}
          </div>
        </div>
      </header>
    </>
  );
}
