import { Injectable, inject } from "@angular/core";
import { isStaffPresenceGateError } from "@adeni/shared";
import { BusinessApiService } from "./business-api.service";
import { BusinessActiveLocationService } from "./business-active-location.service";

/** One-shot on-site verification for sensitive customer surfaces. */
@Injectable({ providedIn: "root" })
export class BusinessPresenceVerifyService {
  private readonly api = inject(BusinessApiService);
  private readonly activeLocation = inject(BusinessActiveLocationService);

  isPresenceGateError(error: unknown): boolean {
    return isStaffPresenceGateError(error);
  }

  async verifyAtActiveLocation(): Promise<void> {
    await this.api.withAuthorizedClient(async (client) => {
      const profile = await client.getTenantProfile().catch(() => null);
      const locationId = this.activeLocation.resolveContextLocation(profile)?.id;
      if (!locationId) {
        throw new Error("no_location");
      }
      await client.verifyPresence({ businessLocationId: locationId });
    });
  }
}
