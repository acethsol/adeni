import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { ServiceOffering, StaffMember } from "@adeni/shared";
import { AdeniConfirmService, AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

type StaffDraft = {
  displayName: string;
  title: string;
  bio: string;
  sortOrder: string;
  isActive: boolean;
  serviceOfferingIds: string[];
};

@Component({
  selector: "app-staff",
  standalone: true,
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./staff.component.html",
  styleUrl: "./staff.component.scss",
})
export class StaffComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly staff = signal<StaffMember[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);

  draft: StaffDraft = this.emptyDraft();

  ngOnInit(): void {
    void this.load();
  }

  emptyDraft(): StaffDraft {
    return {
      displayName: "",
      title: "",
      bio: "",
      sortOrder: "0",
      isActive: true,
      serviceOfferingIds: [],
    };
  }

  serviceName(id: string): string {
    return this.services().find((s) => s.id === id)?.name ?? "Service";
  }

  serviceNames(ids: string[]): string {
    return ids.map((id) => this.serviceName(id)).join(" · ");
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          const [staff, catalog] = await Promise.all([
            c.listTenantStaff(),
            c.getTenantServiceCatalog(),
          ]);
          this.staff.set(staff);
          this.services.set(catalog.items.filter((s) => s.isActive !== false));
        });
      }, "Loading team…");
    } catch {
      this.error.set("Could not load staff. Try again.");
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    this.editingId.set(null);
    this.draft = this.emptyDraft();
    this.showForm.set(true);
  }

  openEdit(member: StaffMember): void {
    this.editingId.set(member.id);
    this.draft = {
      displayName: member.displayName,
      title: member.title ?? "",
      bio: member.bio ?? "",
      sortOrder: String(member.sortOrder ?? 0),
      isActive: member.isActive,
      serviceOfferingIds: [...member.serviceOfferingIds],
    };
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.draft = this.emptyDraft();
  }

  toggleService(serviceId: string): void {
    const current = this.draft.serviceOfferingIds;
    if (current.includes(serviceId)) {
      this.draft.serviceOfferingIds = current.filter((id) => id !== serviceId);
    } else {
      this.draft.serviceOfferingIds = [...current, serviceId];
    }
  }

  async save(): Promise<void> {
    const name = this.draft.displayName.trim();
    if (name.length < 2) {
      this.feedback.error("Display name needs at least 2 characters.");
      return;
    }

    const sortOrder = Number.parseInt(this.draft.sortOrder, 10) || 0;
    const editingId = this.editingId();
    this.busy.set(editingId ?? "create");

    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          if (editingId) {
            await c.updateTenantStaff(editingId, {
              displayName: name,
              title: this.draft.title.trim() || null,
              bio: this.draft.bio.trim() || null,
              sortOrder,
              isActive: this.draft.isActive,
            });
            await c.replaceTenantStaffServices(editingId, {
              serviceOfferingIds: this.draft.serviceOfferingIds,
            });
          } else {
            await c.createTenantStaff({
              displayName: name,
              title: this.draft.title.trim() || null,
              bio: this.draft.bio.trim() || null,
              sortOrder,
              serviceOfferingIds: this.draft.serviceOfferingIds,
            });
          }
        });
      }, editingId ? "Saving…" : "Adding…");

      this.feedback.success(editingId ? "Team member updated." : "Team member added.");
      this.cancelForm();
      await this.load();
    } catch {
      this.feedback.error("Could not save team member.");
    } finally {
      this.busy.set(null);
    }
  }

  async deactivate(member: StaffMember): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Deactivate team member",
      message: `Hide ${member.displayName} from online booking? Existing bookings stay assigned.`,
      confirmLabel: "Deactivate",
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.busy.set(member.id);
    try {
      await this.api.withAuthorizedClient((c) => c.deactivateTenantStaff(member.id));
      this.feedback.success(`${member.displayName} is no longer bookable online.`);
      await this.load();
    } catch {
      this.feedback.error("Could not deactivate.");
    } finally {
      this.busy.set(null);
    }
  }
}
