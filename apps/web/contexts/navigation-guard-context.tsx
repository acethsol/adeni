"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  type ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import { useConfirm } from "@/contexts/confirm-context";

type GuardCheck = () => boolean;

type NavigationGuardContextValue = {
  register: (check: GuardCheck) => () => void;
};

const NavigationGuardContext = createContext<NavigationGuardContextValue | null>(null);

export function NavigationGuardProvider({ children }: { children: ReactNode }) {
  const guardsRef = useRef<Set<GuardCheck>>(new Set());
  const bypassRef = useRef(false);
  const confirm = useConfirm();
  const router = useRouter();

  const isAnyDirty = useCallback(() => {
    for (const check of guardsRef.current) {
      if (check()) return true;
    }
    return false;
  }, []);

  useEffect(() => {
    function onBeforeUnload(event: BeforeUnloadEvent) {
      if (isAnyDirty()) {
        event.preventDefault();
        event.returnValue = "";
      }
    }

    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [isAnyDirty]);

  useEffect(() => {
    function findAnchor(target: EventTarget | null): HTMLAnchorElement | null {
      if (!(target instanceof Element)) return null;
      return target.closest("a[href]");
    }

    function onClick(event: MouseEvent) {
      if (bypassRef.current) return;
      if (event.defaultPrevented || event.button !== 0) return;
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      const anchor = findAnchor(event.target);
      if (!anchor) return;

      const href = anchor.getAttribute("href");
      if (!href || href.startsWith("#") || href.startsWith("mailto:") || href.startsWith("tel:")) {
        return;
      }
      if (anchor.target && anchor.target !== "_self") return;
      if (!isAnyDirty()) return;

      event.preventDefault();
      event.stopImmediatePropagation();

      void confirm({
        title: "Leave without saving?",
        description: "You have unsaved changes on this page. If you leave now, they'll be lost.",
        confirmLabel: "Leave page",
        cancelLabel: "Stay on page",
        tone: "destructive",
      }).then((confirmed) => {
        if (!confirmed) return;
        bypassRef.current = true;
        router.push(href);
        window.setTimeout(() => {
          bypassRef.current = false;
        }, 300);
      });
    }

    document.addEventListener("click", onClick, true);
    return () => document.removeEventListener("click", onClick, true);
  }, [confirm, isAnyDirty, router]);

  const value = useMemo<NavigationGuardContextValue>(
    () => ({
      register: (check) => {
        guardsRef.current.add(check);
        return () => {
          guardsRef.current.delete(check);
        };
      },
    }),
    [],
  );

  return <NavigationGuardContext.Provider value={value}>{children}</NavigationGuardContext.Provider>;
}

/**
 * Warns the user with a confirmation dialog before they navigate away (via in-app
 * links) or close/refresh the tab while `isDirty` is true.
 */
export function useUnsavedChangesGuard(isDirty: boolean) {
  const context = useContext(NavigationGuardContext);
  if (!context) {
    throw new Error("useUnsavedChangesGuard must be used within NavigationGuardProvider");
  }

  const dirtyRef = useRef(isDirty);

  useEffect(() => {
    dirtyRef.current = isDirty;
  }, [isDirty]);

  useEffect(() => {
    return context.register(() => dirtyRef.current);
  }, [context]);
}
