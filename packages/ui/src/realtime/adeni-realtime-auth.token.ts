import { InjectionToken } from "@angular/core";

export type AdeniRealtimeHub = "messaging" | "operations";

/** App-specific hub URL + JWT (or dev query) wiring for SignalR. */
export interface AdeniRealtimeAuth {
  getHubUrl(hub: AdeniRealtimeHub): string;
  getAccessToken(): Promise<string>;
}

export const ADENI_REALTIME_AUTH = new InjectionToken<AdeniRealtimeAuth>("ADENI_REALTIME_AUTH");
