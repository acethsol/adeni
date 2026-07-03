import { createContext, useContext, type ReactNode } from "react";
import { useActiveMarket, type ActiveMarketState } from "@/lib/market";

const MarketContext = createContext<ActiveMarketState | null>(null);

export function MarketProvider({ children }: { children: ReactNode }) {
  const value = useActiveMarket();
  return <MarketContext.Provider value={value}>{children}</MarketContext.Provider>;
}

export function useMarket(): ActiveMarketState {
  const context = useContext(MarketContext);
  if (!context) {
    throw new Error("useMarket must be used within MarketProvider");
  }

  return context;
}
