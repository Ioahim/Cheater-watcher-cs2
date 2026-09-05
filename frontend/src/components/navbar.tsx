"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navLinks = [
  { href: "/matches", label: "Matches" },
  { href: "/stats", label: "Stats" },
  { href: "/accounts", label: "Accounts" },
];

export function Navbar() {
  const pathname = usePathname();

  return (
    <header className="bg-surface">
      <div className="mx-auto flex h-16 max-w-6xl items-center gap-4 px-4">
        <Link href="/" className="flex items-center gap-2">
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
      </div>
    </header>
  );
}
