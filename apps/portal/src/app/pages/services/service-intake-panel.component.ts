import { Component, DestroyRef, effect, inject, input, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { IntakeSummary } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFilterSelectComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniFilterSelectOption,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-service-intake-panel",
  standalone: true,
  imports: [
    RouterLink,
    AdeniCarbonIconComponent,
    AdeniFilterSelectComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
    AdeniStaffButtonComponent,
  ],
  templateUrl: "./service-intake-panel.component.html",
  styleUrl: "./service-intake-panel.component.scss",
})
export class ServiceIntakePanelComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly serviceId = input.required<string>();
  readonly serviceName = input.required<string>();
  readonly canManageForms = input(false);

  readonly templates = signal<IntakeSummary[]>([]);
  readonly bindingTemplateId = signal<string | null>(null);
  readonly bindingRevision = signal<string | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);

  readonly assignableFormOptions = signal<AdeniFilterSelectOption[]>([]);

  constructor() {
    const reload = effect(() => {
      const id = this.serviceId();
      void this.loadForService(id);
    });
    this.destroyRef.onDestroy(() => reload.destroy());
  }

  templateLabel(templateId: string | null): string {
    if (!templateId) {
      return this.tr("intake.unassigned");
    }
    return this.templates().find((row) => row.id === templateId)?.name ?? this.tr("intake.form");
  }

  assignedFormPublished(): boolean {
    const id = this.bindingTemplateId();
    if (!id) {
      return false;
    }
    const row = this.templates().find((r) => r.id === id);
    return Boolean(row?.isActive && row.publishedVersionId);
  }

  async loadForService(serviceId: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const [rows, binding] = await Promise.all([
          c.intake.listTemplates(1),
          c.intake.getBinding(serviceId),
        ]);
        this.templates.set(rows);
        this.bindingTemplateId.set(binding.templateId);
        this.bindingRevision.set(binding.revision);
        this.assignableFormOptions.set([
          { value: "", label: this.tr("intake.notRequired") },
          ...rows
            .filter((row) => row.isActive && row.publishedVersionId)
            .map((row) => ({ value: row.id, label: row.name })),
        ]);
      });
    } catch (e) {
      this.error.set(this.apiErr(e, "intake.loadError"));
    } finally {
      this.loading.set(false);
    }
  }

  async onFormChange(templateId: string): Promise<void> {
    const revision = this.bindingRevision();
    if (!revision) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const value = await this.api.withAuthorizedClient((c) =>
        c.intake.setBinding(this.serviceId(), templateId || null, revision),
      );
      this.bindingTemplateId.set(value.templateId);
      this.bindingRevision.set(value.revision);
    } catch (e) {
      this.error.set(this.apiErr(e, "intake.loadError"));
    } finally {
      this.busy.set(false);
    }
  }
}
