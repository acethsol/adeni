import { createApiClient } from "@/lib/adeni";
import { listMarkets, mapApiMarketToConfig, type MarketConfig } from "@adeni/shared";

export async function getMarkets(): Promise<MarketConfig[]> {
  try {
    const client = createApiClient();
    return await client.getMarkets();
  } catch {
    return listMarkets();
  }
}
