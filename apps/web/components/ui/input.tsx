import type { InputHTMLAttributes, TextareaHTMLAttributes } from "react";
import { cn } from "@/lib/cn";

type Props = InputHTMLAttributes<HTMLInputElement> & {
  label?: string;
  hint?: string;
  error?: string;
};

export function Input({ label, hint, error, className, id, ...rest }: Props) {
  const inputId = id ?? label?.toLowerCase().replace(/\s+/g, "-");

  return (
    <div className="space-y-2">
      {label ? (
        <label htmlFor={inputId} className="text-sm font-semibold text-muted-foreground">
          {label}
        </label>
      ) : null}
      <input
        id={inputId}
        className={cn(
          "w-full rounded-xl border border-border-strong bg-surface px-4 py-3 text-sm text-foreground outline-none transition-shadow focus:border-accent focus:ring-2 focus:ring-accent/20",
          error && "border-destructive/40 focus:border-destructive focus:ring-destructive/20",
          className,
        )}
        {...rest}
      />
      {hint && !error ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
    </div>
  );
}

export function Textarea({
  label,
  hint,
  error,
  className,
  id,
  ...rest
}: Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, "id"> & {
  label?: string;
  hint?: string;
  error?: string;
  id?: string;
}) {
  const inputId = id ?? label?.toLowerCase().replace(/\s+/g, "-");

  return (
    <div className="space-y-2">
      {label ? (
        <label htmlFor={inputId} className="text-sm font-semibold text-muted-foreground">
          {label}
        </label>
      ) : null}
      <textarea
        id={inputId}
        className={cn(
          "min-h-24 w-full rounded-xl border border-border-strong bg-surface px-4 py-3 text-sm text-foreground outline-none transition-shadow focus:border-accent focus:ring-2 focus:ring-accent/20",
          error && "border-destructive/40 focus:border-destructive focus:ring-destructive/20",
          className,
        )}
        {...rest}
      />
      {hint && !error ? <p className="text-xs text-muted-foreground">{hint}</p> : null}
      {error ? <p className="text-xs text-destructive">{error}</p> : null}
    </div>
  );
}
