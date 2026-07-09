type Props = {
  rating: number;
  max?: number;
  size?: "sm" | "md";
};

export function StarRating({ rating, max = 5, size = "sm" }: Props) {
  const starClass = size === "sm" ? "text-sm" : "text-base";

  return (
    <span className={`inline-flex items-center gap-0.5 text-[#f59e0b] ${starClass}`} aria-label={`${rating} out of ${max} stars`}>
      {Array.from({ length: max }, (_, index) => (
        <span key={index}>{index < Math.round(rating) ? "★" : "☆"}</span>
      ))}
    </span>
  );
}
