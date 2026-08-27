import Link from "next/link";

export function AuthBanner() {
  return (
    <div className="flex flex-wrap items-center gap-4 rounded-xl border border-amber-500/30 bg-amber-500/10 px-5 py-4">
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="size-5 shrink-0 text-amber-400"
        aria-hidden
      >
        <path d="m21.73 18-8-14a2 2 0 0 0-3.48 0l-8 14A2 2 0 0 0 4 21h16a2 2 0 0 0 1.73-3Z" />
        <path d="M12 9v4M12 17h.01" />
      </svg>
      <p className="min-w-0 flex-1 text-sm">
        <span className="font-semibold text-amber-400">Authentication required:</span>{" "}
        <span className="text-muted">
          Sign in and link your Steam account to enable match tracking.
        </span>
      </p>
      <Link
        href="/login"
        className="rounded-lg bg-amber-500 px-4 py-2 text-sm font-semibold text-deep transition-colors hover:bg-amber-400"
      >
        Sign in
      </Link>
    </div>
  );
}
