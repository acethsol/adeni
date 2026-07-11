import type { ReactNode } from "react";
import { PublicFooter } from "@/components/public-footer";
import { getActiveMarketConfig } from "@/lib/market";

type Props = {
  children: ReactNode;
};

export default async function PublicLayout({ children }: Props) {
  const market = await getActiveMarketConfig();

  return (
    <div className="flex min-h-screen flex-col bg-background text-foreground">
      <div className="flex flex-1 flex-col">{children}</div>
        <PublicFooter
          marketId={market.id}
          currency={market.currency}
          countryCode={market.countryCode}
        />
    </div>
  );
}
