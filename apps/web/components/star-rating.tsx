type Props = {
  rating: number;
  max?: number;
  size?: "sm" | "md";
  tone?: "amber" | "accent";
};

export function StarRating({ rating, max = 5, size = "sm", tone = "amber" }: Props) {
  const starClass = size === "sm" ? "text-sm" : "text-base";
  const toneClass = tone === "accent" ? "text-accent" : "text-[#f59e0b]";

  return (
    <span
      className={`inline-flex items-center gap-0.5 ${toneClass} ${starClass}`}
      aria-label={`${rating} out of ${max} stars`}
    >
      {Array.from({ length: max }, (_, index) => (
        <span key={index}>{index < Math.round(rating) ? "★" : "☆"}</span>
      ))}
    </span>
  );
}
