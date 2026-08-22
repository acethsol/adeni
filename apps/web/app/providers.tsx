"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { NavigationProgress } from "@/components/navigation-progress";
import { ActionLoadingProvider } from "@/contexts/action-loading-context";
import { ConfirmProvider } from "@/contexts/confirm-context";
import { NavigationGuardProvider } from "@/contexts/navigation-guard-context";
import { ToastProvider } from "@/contexts/toast-context";

export function AppProviders({ children }: { children: ReactNode }) {
  const [queryClient] = useState(
    () =>
      new QueryClient({
        defaultOptions: {
          queries: {
            staleTime: 30_000,
            refetchOnWindowFocus: false,
            retry: 1,
          },
        },
      }),
  );

  return (
    <QueryClientProvider client={queryClient}>
      <ToastProvider>
        <ConfirmProvider>
          <NavigationGuardProvider>
            <ActionLoadingProvider>
              <NavigationProgress />
              {children}
            </ActionLoadingProvider>
          </NavigationGuardProvider>
        </ConfirmProvider>
      </ToastProvider>
    </QueryClientProvider>
  );
}
