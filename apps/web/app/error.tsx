"use client";

import { useEffect } from "react";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function RootError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div id="main-content" className="flex min-h-screen flex-col items-center justify-center bg-background px-6 text-center">
      <div className="flex h-14 w-14 items-center justify-center rounded-full bg-destructive-bg text-destructive">
        <AlertTriangle className="h-7 w-7" aria-hidden />
      </div>
      <h1 className="mt-6 text-2xl font-bold text-foreground">Something went wrong</h1>
      <p className="mt-3 max-w-md text-sm leading-relaxed text-muted">
        An unexpected error interrupted this page. You can try again, or head back home if the
        problem keeps happening.
      </p>
      {error.digest ? (
        <p className="mt-2 text-xs text-muted-foreground">Reference: {error.digest}</p>
      ) : null}
      <div className="mt-8 flex flex-wrap justify-center gap-3">
        <Button variant="secondary" href="/">
          Go home
        </Button>
        <Button onClick={reset}>Try again</Button>
      </div>
    </div>
  );
}
