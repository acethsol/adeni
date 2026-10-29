import { Injectable, inject, signal } from "@angular/core";
import {
  hasBusinessPermission,
  normalizeBusinessPermissionRole,
  permissionsForRole,
  type BusinessPermission,
  type BusinessPermissionRole,
} from "@adeni/shared";
import { BusinessApiService } from "./business-api.service";

@Injectable({ providedIn: "root" })
export class PortalSessionService {
  private readonly api = inject(BusinessApiService);

  readonly permissionRole = signal<BusinessPermissionRole>("owner");
  readonly permissions = signal<readonly BusinessPermission[]>([]);
  readonly staffMemberId = signal<string | null>(null);
  readonly loaded = signal(false);

  async ensureLoaded(): Promise<void> {
    if (this.loaded()) {
      return;
    }
    await this.refresh();
  }

  async refresh(): Promise<void> {
    try {
      const context = await this.api.withAuthorizedClient((client) => client.getBusinessContext());
      const role = normalizeBusinessPermissionRole(context.permissionRole);
      const fromApi = (context.permissions ?? []) as BusinessPermission[];
      this.permissionRole.set(role);
      this.permissions.set(fromApi.length > 0 ? fromApi : permissionsForRole(role));
      this.staffMemberId.set(context.staffMemberId ?? null);
    } catch {
      // Fail open for owners mid-onboarding (no context yet) — full nav until profile loads.
      this.permissionRole.set("owner");
      this.permissions.set(permissionsForRole("owner"));
      this.staffMemberId.set(null);
    } finally {
      this.loaded.set(true);
    }
  }

  can(required: BusinessPermission | readonly BusinessPermission[] | undefined): boolean {
    if (!required) {
      return true;
    }
    return hasBusinessPermission(this.permissions(), required);
  }
}
