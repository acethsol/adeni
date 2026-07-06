"use client";

import { useRouter } from "next/navigation";
import { FormEvent, useState } from "react";
import { Search } from "lucide-react";
import { cn } from "@/lib/cn";

type Props = {
  className?: string;
  defaultValue?: string;
  placeholder?: string;
};

export function GlobalSearchBar({
  className,
  defaultValue = "",
  placeholder = "Search services, areas, businesses…",
}: Props) {
  const router = useRouter();
  const [value, setValue] = useState(defaultValue);

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    const trimmed = value.trim();
    if (!trimmed) {
      router.push("/discover");
      return;
    }

    router.push(`/discover?q=${encodeURIComponent(trimmed)}`);
  }

  return (
    <form onSubmit={handleSubmit} className={cn("relative w-full max-w-md", className)}>
      <Search
        className="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
        aria-hidden
      />
      <input
        type="search"
        value={value}
        onChange={(event) => setValue(event.target.value)}
        placeholder={placeholder}
        className="w-full rounded-full border border-border-strong bg-surface py-2.5 pl-10 pr-4 text-sm text-foreground shadow-sm outline-none transition-shadow placeholder:text-muted-foreground focus:border-accent focus:ring-2 focus:ring-accent/20"
        aria-label="Search Adeni"
      />
    </form>
  );
}
