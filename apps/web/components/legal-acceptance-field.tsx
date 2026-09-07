import Link from "next/link";

type Props = {
  checked: boolean;
  onChange: (checked: boolean) => void;
  id?: string;
  error?: string | null;
  /** Shown on booking checkout when a deposit may be collected. */
  includePaymentsNote?: boolean;
};

export function LegalAcceptanceField({
  checked,
  onChange,
  id = "legal-acceptance",
  error,
  includePaymentsNote = false,
}: Props) {
  return (
    <div className="mt-4">
      <label htmlFor={id} className="flex items-start gap-3 text-sm text-[#1b4332]/80">
        <input
          id={id}
          type="checkbox"
          checked={checked}
          onChange={(event) => onChange(event.target.checked)}
          className="mt-0.5 h-4 w-4 shrink-0 rounded border-[#1b4332]/30 text-[#40916c] focus:ring-[#40916c]/30"
          required
        />
        <span>
          I agree to the{" "}
          <Link href="/terms" className="font-medium text-[#40916c] underline" target="_blank">
            Terms of Service
          </Link>{" "}
          and{" "}
          <Link href="/privacy" className="font-medium text-[#40916c] underline" target="_blank">
            Privacy Policy
          </Link>
          {includePaymentsNote ? (
            <>
              . Payments are processed by licensed providers; Adeni does not hold funds.
            </>
          ) : (
            "."
          )}
        </span>
      </label>
      {error ? <p className="mt-2 text-sm text-red-700">{error}</p> : null}
    </div>
  );
}
