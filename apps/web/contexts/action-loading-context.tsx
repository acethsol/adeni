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
import { ActionLoadingOverlay } from "@/components/loading-panel";

type ActionLoadingContextValue = {
  isActive: boolean;
  message: string;
  start: (message?: string) => void;
  stop: () => void;
  run: <T>(message: string, action: () => Promise<T>) => Promise<T>;
};

const ActionLoadingContext = createContext<ActionLoadingContextValue | null>(null);

export function ActionLoadingProvider({ children }: { children: ReactNode }) {
  const depthRef = useRef(0);
  const [message, setMessage] = useState("Working…");
  const [isActive, setIsActive] = useState(false);

  const start = useCallback((nextMessage = "Working…") => {
    depthRef.current += 1;
    setMessage(nextMessage);
    setIsActive(true);
  }, []);

  const stop = useCallback(() => {
    depthRef.current = Math.max(0, depthRef.current - 1);
    if (depthRef.current === 0) {
      setIsActive(false);
    }
  }, []);

  const run = useCallback(
    async <T,>(nextMessage: string, action: () => Promise<T>) => {
      start(nextMessage);
      try {
        return await action();
      } finally {
        stop();
      }
    },
    [start, stop],
  );

  const value = useMemo(
    () => ({
      isActive,
      message,
      start,
      stop,
      run,
    }),
    [isActive, message, run, start, stop],
  );

  return (
    <ActionLoadingContext.Provider value={value}>
      {children}
      <ActionLoadingOverlay message={message} visible={isActive} />
    </ActionLoadingContext.Provider>
  );
}

export function useActionLoading() {
  const context = useContext(ActionLoadingContext);
  if (!context) {
    throw new Error("useActionLoading must be used within ActionLoadingProvider");
  }

  return context;
}
