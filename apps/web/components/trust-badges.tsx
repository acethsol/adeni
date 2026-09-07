import { VERIFICATION_BADGE_LABELS } from "@adeni/shared";

type Props = {
  badges?: string[] | null;
  verifiedSince?: string | null;
  completionRate?: number | null;
  compact?: boolean;
};

export function TrustBadges({ badges, verifiedSince, completionRate, compact = false }: Props) {
  const items = badges ?? [];

  if (items.length === 0 && !verifiedSince && completionRate == null) {
    return null;
  }

  return (
    <div className={`flex flex-wrap items-center gap-2 ${compact ? "text-[11px]" : "text-xs"}`}>
      {items.map((badge) => (
        <span
          key={badge}
          className="rounded-full border border-accent/30 bg-accent/10 px-2 py-0.5 font-semibold text-accent"
        >
          {VERIFICATION_BADGE_LABELS[badge] ?? badge}
        </span>
      ))}
      {verifiedSince ? (
        <span className="text-muted">
          Verified since {new Date(verifiedSince).toLocaleDateString(undefined, { month: "short", year: "numeric" })}
        </span>
      ) : null}
      {completionRate != null ? (
        <span className="text-muted">{Math.round(completionRate * 100)}% completion rate</span>
      ) : null}
    </div>
  );
}
