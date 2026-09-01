import Image from "next/image";
import { useId } from "react";
import type { Rank } from "@/lib/types";

const COMPETITIVE_RANKS = [
  "Silver I",
  "Silver II",
  "Silver III",
  "Silver IV",
  "Silver Elite",
  "Silver Elite Master",
  "Gold Nova I",
  "Gold Nova II",
  "Gold Nova III",
  "Gold Nova Master",
  "Master Guardian I",
  "Master Guardian II",
  "Master Guardian Elite",
  "Distinguished Master Guardian",
  "Legendary Eagle",
  "Legendary Eagle Master",
  "Supreme Master First Class",
  "Global Elite",
];

const PREMIER_TIERS = [
  { min: 30000, dark: "#92700c", accent: "#eab308", light: "#ffd700" },
  { min: 25000, dark: "#7f1d1d", accent: "#eb4b4b", light: "#ef4444" },
  { min: 20000, dark: "#be185d", accent: "#d32ce6", light: "#ec4899" },
  { min: 15000, dark: "#6b21a8", accent: "#8847ff", light: "#a855f7" },
  { min: 10000, dark: "#1e3a6e", accent: "#4c6aff", light: "#3b82f6" },
  { min: 5000, dark: "#5a7d95", accent: "#8bb8d0", light: "#5e98d9" },
  { min: 0, dark: "#6b7280", accent: "#6B6A6A", light: "#9ca3af" },
];

function darken(hex: string, amount: number): string {
  const n = parseInt(hex.replace("#", ""), 16);
  const r = Math.max(0, Math.round(((n >> 16) & 0xff) * (1 - amount)));
  const g = Math.max(0, Math.round(((n >> 8) & 0xff) * (1 - amount)));
  const b = Math.max(0, Math.round((n & 0xff) * (1 - amount)));
  return `#${((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1)}`;
}

export function PremierBadge({ rating }: { rating: number }) {
  const uid = useId();
  const tier =
    PREMIER_TIERS.find((t) => rating >= t.min) ??
    PREMIER_TIERS[PREMIER_TIERS.length - 1];
  const label = `Premier ${rating.toLocaleString("en-US")}`;
  const overlayDark = darken(tier.dark, 0.25);
  const overlayDarkest = darken(tier.dark, 0.55);

  return (
    <span
      role="img"
      aria-label={label}
      title={label}
      className="inline-flex h-6 items-center"
    >
      <svg
        viewBox="0 0 178 64"
        className="h-6 w-auto"
        fill="none"
        xmlns="http://www.w3.org/2000/svg"
      >
        <g clipPath={`url(#c${uid})`}>
          <path d="M25 0H21L9 64H13L25 0Z" fill={tier.accent} />
          <path
            d="M178 0H33.9996L22 64H166L178 0Z"
            fill={`url(#p0${uid})`}
          />
          <path
            d="M176.25 1.5H33.24L21.6562 62.5H164.666L176.25 1.5Z"
            fill={`url(#p1${uid})`}
          />
          <path
            opacity="0.4"
            d="M46.1141 4L54 4L40.8859 61H33L46.1141 4Z"
            fill={tier.accent}
          />
          <path d="M36.7301 4L42 4L30.2699 61H25L36.7301 4Z" fill={tier.accent} />
          <path
            opacity="0.4"
            d="M56.8737 4L72 4L59.1263 61H44L56.8737 4Z"
            fill={tier.accent}
          />
          <path
            opacity="0.4"
            d="M75.7813 4L110 4L97.2187 61H63L75.7813 4Z"
            fill={tier.accent}
          />
          <path d="M18 0H27L18 64H3.25L18 0Z" fill="#3A3A3A" />
          <path d="M12 0H21L9 64H0L12 0Z" fill="white" />
          <path d="M24.9997 0H33.9997L22 64H13L24.9997 0Z" fill="white" />
          <path d="M25 0H33L21 64H13L25 0Z" fill={`url(#p2${uid})`} />
          <path d="M25 0H33L21 64H13L25 0Z" fill={`url(#p3${uid})`} />
          <path d="M12 0H20L8 64H0L12 0Z" fill={`url(#p4${uid})`} />
          <path d="M12 0H20L8 64H0L12 0Z" fill={`url(#p5${uid})`} />
        </g>
        <text
          x="100"
          y="42"
          textAnchor="middle"
          fontSize="24"
          fontWeight="700"
          fill="white"
          style={{ fontFamily: "var(--font-mono)" }}
        >
          {rating.toLocaleString("en-US")}
        </text>
        <defs>
          <clipPath id={`c${uid}`}>
            <rect width="178" height="64" fill="white" />
          </clipPath>
          <linearGradient
            id={`p0${uid}`}
            x1="187.49"
            y1="48.7288"
            x2="30.4973"
            y2="20.5012"
            gradientUnits="userSpaceOnUse"
          >
            <stop offset="0.9053" stopColor={tier.light} />
            <stop offset="1" stopColor={tier.dark} />
          </linearGradient>
          <linearGradient
            id={`p1${uid}`}
            x1="185.411"
            y1="47.9446"
            x2="26.5628"
            y2="33.7951"
            gradientUnits="userSpaceOnUse"
          >
            <stop offset="0.862691" stopColor={overlayDark} stopOpacity="0.55" />
            <stop offset="1" stopColor={overlayDarkest} />
          </linearGradient>
          <linearGradient
            id={`p2${uid}`}
            x1="23.4998"
            y1="1"
            x2="23.4998"
            y2="63"
            gradientUnits="userSpaceOnUse"
          >
            <stop stopColor={tier.light} />
            <stop offset="1" stopColor={tier.dark} />
          </linearGradient>
          <linearGradient
            id={`p3${uid}`}
            x1="23.4998"
            y1="1"
            x2="23.4998"
            y2="63"
            gradientUnits="userSpaceOnUse"
          >
            <stop stopColor={tier.light} />
            <stop offset="1" stopColor={tier.dark} />
          </linearGradient>
          <linearGradient
            id={`p4${uid}`}
            x1="10.4998"
            y1="1"
            x2="10.4998"
            y2="63"
            gradientUnits="userSpaceOnUse"
          >
            <stop stopColor={tier.light} />
            <stop offset="1" stopColor={tier.dark} />
          </linearGradient>
          <linearGradient
            id={`p5${uid}`}
            x1="10.4998"
            y1="1"
            x2="10.4998"
            y2="63"
            gradientUnits="userSpaceOnUse"
          >
            <stop stopColor={tier.light} />
            <stop offset="1" stopColor={tier.dark} />
          </linearGradient>
        </defs>
      </svg>
    </span>
  );
}

export function CompetitiveBadge({ level }: { level: number }) {
  const name = COMPETITIVE_RANKS[level - 1] ?? `Rank ${level}`;
  return (
    <Image
      src={`/ranks/competitive/${level}.svg`}
      alt={name}
      title={name}
      width={32}
      height={13}
      unoptimized
      className="h-6 w-14"
    />
  );
}

export function WingmanBadge({ level }: { level: number }) {
  const name = `Wingman ${COMPETITIVE_RANKS[level - 1] ?? `Rank ${level}`}`;
  return (
    <Image
      src={`/ranks/wingman/${level}.svg`}
      alt={name}
      title={name}
      width={32}
      height={13}
      unoptimized
      className="h-6 w-14"
    />
  );
}

export function UnrankedBadge() {
  return (
    <Image
      src="/ranks/competitive/0.svg"
      alt="Unranked"
      title="Unranked"
      width={32}
      height={13}
      unoptimized
      className="h-6 w-14 opacity-60"
    />
  );
}

export function RankBadge({ rank }: { rank: Rank | null }) {
  if (!rank) return null;
  if (rank.kind === "premier") return <PremierBadge rating={rank.rating} />;
  if (rank.kind === "wingman") return <WingmanBadge level={rank.level} />;
  return <CompetitiveBadge level={rank.level} />;
}
