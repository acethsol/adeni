"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { TriangleAlert } from "lucide-react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/cn";

type ConfirmTone = "default" | "destructive";

type ConfirmOptions = {
  title: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  tone?: ConfirmTone;
};

type ConfirmState = ConfirmOptions & { open: boolean };

type ConfirmContextValue = (options: ConfirmOptions) => Promise<boolean>;

const ConfirmContext = createContext<ConfirmContextValue | null>(null);

const initialState: ConfirmState = { open: false, title: "" };

export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<ConfirmState>(initialState);
  const resolverRef = useRef<((confirmed: boolean) => void) | null>(null);

  const confirm = useCallback((options: ConfirmOptions) => {
    return new Promise<boolean>((resolve) => {
      resolverRef.current = resolve;
      setState({ ...options, open: true });
    });
  }, []);

  const resolve = useCallback((confirmed: boolean) => {
    resolverRef.current?.(confirmed);
    resolverRef.current = null;
    setState(initialState);
  }, []);

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      {state.open ? (
        <div
          className="fixed inset-0 z-[220] flex items-center justify-center bg-primary/20 px-6 backdrop-blur-[2px]"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirm-dialog-title"
          onClick={() => resolve(false)}
        >
          <div
            className="w-full max-w-sm rounded-2xl border border-border bg-surface p-6 shadow-xl"
            onClick={(event) => event.stopPropagation()}
          >
            <div
              className={cn(
                "flex h-11 w-11 items-center justify-center rounded-full",
                state.tone === "destructive" ? "bg-destructive-bg text-destructive" : "bg-accent/10 text-accent",
              )}
            >
              <TriangleAlert className="h-5 w-5" aria-hidden />
            </div>
            <h2 id="confirm-dialog-title" className="mt-4 text-lg font-semibold text-foreground">
              {state.title}
            </h2>
            {state.description ? (
              <p className="mt-2 text-sm leading-relaxed text-muted">{state.description}</p>
            ) : null}
            <div className="mt-6 flex justify-end gap-2">
              <Button variant="secondary" size="sm" onClick={() => resolve(false)}>
                {state.cancelLabel ?? "Cancel"}
              </Button>
              <Button
                variant={state.tone === "destructive" ? "destructive" : "primary"}
                size="sm"
                onClick={() => resolve(true)}
              >
                {state.confirmLabel ?? "Confirm"}
              </Button>
            </div>
          </div>
        </div>
      ) : null}
    </ConfirmContext.Provider>
  );
}

export function useConfirm() {
  const context = useContext(ConfirmContext);
  if (!context) {
    throw new Error("useConfirm must be used within ConfirmProvider");
  }

  return context;
}
