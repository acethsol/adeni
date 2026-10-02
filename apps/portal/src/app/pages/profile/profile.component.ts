import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BusinessProfile } from "@adeni/shared";
import { formatTenantStatus, resolveBusinessCoverImage } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
import { BusinessApiService } from "../../core/services/business-api.service";

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;
const ALLOWED_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MAX_BYTES = 5 * 1024 * 1024;

@Component({
  selector: "app-profile",
  standalone: true,
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./profile.component.html",
  styleUrl: "./profile.component.scss",
})
export class ProfileComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly saving = signal(false);
  readonly uploading = signal(false);
  readonly coverPreview = signal<string>("");

  businessName = "";
  categorySlug = "";
  phone = "";
  description = "";
  autoConfirm = false;
  depositPercent = 0;

  readonly formatTenantStatus = formatTenantStatus;

  ngOnInit(): void {
    void this.load();
  }

  canEdit(): boolean {
    const p = this.profile();
    return p ? p.status === 0 || p.status === 3 : false;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const p = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
      this.profile.set(p);
      this.businessName = p.businessName;
      this.categorySlug = p.categorySlug;
      this.phone = p.phone;
      this.description = p.description ?? "";
      this.autoConfirm = p.autoConfirmBookings ?? false;
      this.depositPercent = p.depositPercent ?? 0;
      this.coverPreview.set(resolveBusinessCoverImage(p.categorySlug, p.coverImageUrl));
    } catch {
      this.error.set("Could not load profile.");
      this.profile.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  async saveProfile(): Promise<void> {
    if (!this.canEdit()) return;
    if (!this.businessName.trim() || !this.categorySlug.trim() || !PHONE_PATTERN.test(this.phone.trim())) {
      this.error.set("Fix required fields before saving.");
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((c) =>
        c.updateTenantProfile({
          businessName: this.businessName.trim(),
          categorySlug: this.categorySlug.trim(),
          phone: this.phone.trim(),
          description: this.description.trim(),
        }),
      );
      this.profile.set(updated);
    } catch {
      this.error.set("Could not save profile.");
    } finally {
      this.saving.set(false);
    }
  }

  async saveSettings(): Promise<void> {
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((c) =>
        c.updateTenantSettings({
          autoConfirmBookings: this.autoConfirm,
          depositPercent: this.depositPercent,
        }),
      );
      this.profile.set(updated);
    } catch {
      this.error.set("Could not save booking settings.");
    } finally {
      this.saving.set(false);
    }
  }

  async onCoverSelected(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file || !this.profile()) return;

    if (!ALLOWED_TYPES.has(file.type)) {
      this.error.set("Use JPEG, PNG, or WebP.");
      return;
    }
    if (file.size > MAX_BYTES) {
      this.error.set("Cover must be 5 MB or smaller.");
      return;
    }

    this.uploading.set(true);
    this.error.set(null);
    try {
      const url = await this.api.withAuthorizedClient(async (c) => {
        const slot = await c.createCoverUploadUrl(file.type, file.size);
        const upload = await fetch(slot.uploadUrl, {
          method: "PUT",
          headers: { "Content-Type": file.type },
          body: file,
        });
        if (!upload.ok) throw new Error("upload failed");
        return c.updateTenantCoverImage({ coverImageKey: slot.storageKey });
      });
      this.coverPreview.set(url);
      await this.load();
    } catch {
      this.error.set("Cover upload failed.");
    } finally {
      this.uploading.set(false);
      input.value = "";
    }
  }
}
