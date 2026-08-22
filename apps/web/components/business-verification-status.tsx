import { Ban, Check, Clock3, FileText, ShieldCheck, TriangleAlert } from "lucide-react";
import { VERIFICATION_DOCUMENT_LABELS } from "@adeni/shared";
import type { BusinessProfile } from "@adeni/shared";
import { cn } from "@/lib/cn";

type Props = {
  profile: BusinessProfile;
};

const STEPS = [
  { status: 0, label: "Draft", description: "Set up your details" },
  { status: 1, label: "Pending review", description: "Admin is checking your documents" },
  { status: 2, label: "Verified", description: "Trusted badge is live" },
];

export function BusinessVerificationStatus({ profile }: Props) {
  const { status } = profile;

  if (status === 4) {
    return (
      <div className="rounded-2xl border border-destructive/30 bg-destructive-bg p-5">
        <div className="flex items-center gap-2 text-destructive">
          <Ban className="h-5 w-5 shrink-0" aria-hidden />
          <p className="font-semibold">Account suspended</p>
        </div>
        <p className="mt-2 text-sm text-destructive/90">
          Your business is currently suspended and hidden from customers. Contact support to appeal or resolve this.
        </p>
      </div>
    );
  }

  const activeIndex = status === 3 ? 1 : Math.min(status, 2);

  return (
    <div className="space-y-5">
      <div className="flex items-center">
        {STEPS.map((step, index) => {
          const isRejectedHere = status === 3 && index === 1;
          const isComplete = !isRejectedHere && index < activeIndex;
          const isCurrent = index === activeIndex;
          const isUpcoming = index > activeIndex;

          return (
            <div key={step.label} className="flex flex-1 items-center last:flex-initial">
              <div className="flex flex-col items-center gap-1.5 text-center">
                <div
                  className={cn(
                    "flex h-9 w-9 items-center justify-center rounded-full border-2 transition-colors",
                    isRejectedHere
                      ? "border-destructive bg-destructive-bg text-destructive"
                      : isComplete
                        ? "border-accent bg-accent text-primary-foreground"
                        : isCurrent
                          ? "border-accent bg-accent/10 text-accent"
                          : "border-border bg-surface text-muted-foreground",
                  )}
                >
                  {isRejectedHere ? (
                    <TriangleAlert className="h-4 w-4" aria-hidden />
                  ) : isComplete ? (
                    <Check className="h-4.5 w-4.5" aria-hidden />
                  ) : index === 2 ? (
                    <ShieldCheck className="h-4.5 w-4.5" aria-hidden />
                  ) : (
                    <Clock3 className="h-4.5 w-4.5" aria-hidden />
                  )}
                </div>
                <div className={cn(isUpcoming && "opacity-60")}>
                  <p
                    className={cn(
                      "text-xs font-bold uppercase tracking-wide",
                      isRejectedHere ? "text-destructive" : isCurrent || isComplete ? "text-foreground" : "text-muted-foreground",
                    )}
                  >
                    {isRejectedHere ? "Rejected" : step.label}
                  </p>
                  <p className="mt-0.5 hidden max-w-[7rem] text-[11px] text-muted sm:block">
                    {isRejectedHere ? "Update documents and resubmit" : step.description}
                  </p>
                </div>
              </div>
              {index < STEPS.length - 1 ? (
                <div
                  className={cn(
                    "mx-2 h-0.5 flex-1 rounded-full transition-colors",
                    index < activeIndex ? "bg-accent" : "bg-border",
                  )}
                  aria-hidden
                />
              ) : null}
            </div>
          );
        })}
      </div>

      {status === 3 ? (
        <div className="rounded-xl border border-destructive/30 bg-destructive-bg px-4 py-3 text-sm text-destructive">
          Your last verification submission was rejected. Update your details and documents below, then resubmit.
        </div>
      ) : null}

      <dl className="space-y-3 text-sm">
        <div className="flex items-center justify-between">
          <dt className="text-muted">Locations</dt>
          <dd className="font-medium text-foreground">{profile.locations.length}</dd>
        </div>
        {profile.verificationDocuments.length > 0 ? (
          <div>
            <dt className="mb-1.5 flex items-center gap-1.5 text-muted">
              <FileText className="h-3.5 w-3.5" aria-hidden />
              Submitted documents
            </dt>
            <dd className="space-y-1">
              {profile.verificationDocuments.map((doc) => (
                <p
                  key={`${doc.documentType}-${doc.submittedAt}`}
                  className="flex items-center justify-between rounded-lg bg-subtle/50 px-3 py-2 font-medium text-foreground"
                >
                  <span>{VERIFICATION_DOCUMENT_LABELS[doc.documentType] ?? "Document"}</span>
                  <span className="text-xs font-normal text-muted-foreground">
                    {new Date(doc.submittedAt).toLocaleDateString()}
                  </span>
                </p>
              ))}
            </dd>
          </div>
        ) : null}
      </dl>
    </div>
  );
}
