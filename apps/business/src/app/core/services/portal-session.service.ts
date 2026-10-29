import { Injectable, inject, signal } from "@angular/core";
import {
  hasPortalPermission,
  normalizePortalPermissionRole,
  permissionsForRole,
  type PortalPermission,
  type PortalPermissionRole,
} from "@adeni/shared";
import { BusinessApiService } from "./business-api.service";

@Injectable({ providedIn: "root" })
export class PortalSessionService {
  private readonly api = inject(BusinessApiService);

  readonly permissionRole = signal<PortalPermissionRole>("owner");
  readonly permissions = signal<readonly PortalPermission[]>([]);
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
      const role = normalizePortalPermissionRole(context.permissionRole);
      const fromApi = (context.permissions ?? []) as PortalPermission[];
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

  can(required: PortalPermission | readonly PortalPermission[] | undefined): boolean {
    if (!required) {
      return true;
    }
    return hasPortalPermission(this.permissions(), required);
  }
}
