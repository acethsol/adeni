import { cn } from "@/lib/cn";

type Step = {
  id: string;
  label: string;
};

type Props = {
  steps: Step[];
  currentStepId: string;
  className?: string;
};

export function FlowStepProgress({ steps, currentStepId, className }: Props) {
  const currentIndex = steps.findIndex((step) => step.id === currentStepId);

  return (
    <ol
      className={cn("flex flex-wrap items-center gap-x-1 gap-y-2", className)}
      aria-label="Progress"
    >
      {steps.map((step, index) => {
        const isComplete = index < currentIndex;
        const isCurrent = index === currentIndex;

        return (
          <li key={step.id} className="flex items-center gap-1">
            <span
              className={cn(
                "flex h-7 w-7 items-center justify-center rounded-full text-xs font-bold transition-colors",
                isComplete && "bg-accent text-primary-foreground",
                isCurrent && "bg-primary text-primary-foreground shadow-sm",
                !isComplete && !isCurrent && "bg-subtle text-muted-foreground",
              )}
              aria-current={isCurrent ? "step" : undefined}
            >
              {index + 1}
            </span>
            <span
              className={cn(
                "text-xs font-semibold",
                isCurrent ? "text-foreground" : "text-muted",
              )}
            >
              {step.label}
            </span>
            {index < steps.length - 1 ? (
              <span
                className={cn(
                  "mx-2 hidden h-px w-8 sm:block",
                  index < currentIndex ? "bg-accent/50" : "bg-border",
                )}
                aria-hidden
              />
            ) : null}
          </li>
        );
      })}
    </ol>
  );
}
