import { Component, computed, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import {
  AdeniCarbonIconComponent,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFilterSelectComponent,
  AdeniListPaginationComponent,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniIconBtnComponent,
  AdeniStaffButtonComponent,
  IntakeFieldsComponent,
  PortalPageComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniDataTableColumn,
  type AdeniFilterSelectOption,
} from "@adeni/ui";
import type { IntakeDefinition, IntakeSummary, IntakeTemplate, ServiceOffering } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";

const TEMPLATE_PAGE_SIZE = 50;
const FIELD_TYPES = [0, 1, 2, 3, 4, 5, 6] as const;

@Component({
  selector: "app-intake-manager",
  standalone: true,
  imports: [
    FormsModule,
    RouterLink,
    PortalPageComponent,
    IntakeFieldsComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniPillComponent,
    AdeniListPaginationComponent,
    AdeniModalComponent,
    AdeniFilterSelectComponent,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniStaffButtonComponent,
  ],
  templateUrl: "./intake-manager.component.html",
  styleUrl: "./intake-manager.component.scss",
})
export class IntakeManagerComponent implements OnInit {
  readonly tr = injectAdeniTranslate();
  readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly destroy = inject(DestroyRef);

  readonly rows = signal<IntakeSummary[]>([]);
  readonly selected = signal<IntakeTemplate | null>(null);
  readonly draft = signal<IntakeDefinition | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly serviceBindings = signal<Record<string, string | null>>({});
  readonly serviceId = signal("");
  readonly binding = signal<{ templateId: string | null; revision: string } | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly loading = signal(true);
  readonly page = signal(1);
  readonly name = signal("");
  readonly showCreate = signal(false);
  readonly hasMoreTemplates = signal(false);

  readonly templatePageSize = TEMPLATE_PAGE_SIZE;
  readonly fieldTypes = FIELD_TYPES;

  readonly templateTotalCount = computed(() => {
    const count = this.rows().length;
    const current = this.page();
    if (this.hasMoreTemplates()) {
      return current * TEMPLATE_PAGE_SIZE + 1;
    }
    return (current - 1) * TEMPLATE_PAGE_SIZE + count;
  });

  readonly serviceLinkRows = computed(() =>
    this.services().map((service) => {
      const templateId = this.serviceBindings()[service.id] ?? null;
      return { service, templateId, formName: this.templateLabel(templateId) };
    }),
  );

  readonly assignableFormOptions = computed((): AdeniFilterSelectOption[] => [
    { value: "", label: this.tr("intake.notRequired") },
    ...this.rows()
      .filter((row) => row.isActive && row.publishedVersionId)
      .map((row) => ({ value: row.id, label: row.name })),
  ]);

  readonly assignServiceOptions = computed((): AdeniFilterSelectOption[] => [
    { value: "", label: this.tr("intake.chooseService") },
    ...this.services().map((service) => ({ value: service.id, label: service.name })),
  ]);

  readonly assignedServiceCount = computed(
    () => this.serviceLinkRows().filter((row) => row.templateId).length,
  );

  readonly formTableColumns = computed((): AdeniDataTableColumn<IntakeSummary>[] => [
    {
      id: "name",
      header: this.tr("intake.name"),
      cell: (row) => row.name,
      sortValue: (row) => row.name.toLowerCase(),
    },
    {
      id: "version",
      header: this.tr("intake.versionColumn"),
      cell: () => "",
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
    },
  ]);

  readonly trackFormRow = (row: IntakeSummary): string => row.id;

  private abort = new AbortController();
  private alive = true;
  private managementName = "";

  constructor() {
    this.destroy.onDestroy(() => {
      this.alive = false;
      this.abort.abort();
      this.draft.set(null);
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  templateLabel(templateId: string | null): string {
    if (!templateId) {
      return this.tr("intake.unassigned");
    }
    return this.rows().find((row) => row.id === templateId)?.name ?? this.tr("intake.form");
  }

  readonly editorSubtitle = computed(() => {
    const form = this.selected();
    if (!form) {
      return "";
    }
    return this.rows().find((row) => row.id === form.id)?.name ?? form.published?.title ?? form.draft?.title ?? "";
  });

  readonly serviceLinkColumns = computed((): AdeniDataTableColumn<{ service: ServiceOffering; templateId: string | null; formName: string }>[] => [
    {
      id: "service",
      header: this.tr("common.service"),
      cell: (row) => row.service.name,
      sortValue: (row) => row.service.name.toLowerCase(),
    },
    {
      id: "form",
      header: this.tr("intake.linkedForm"),
      cell: (row) => row.formName,
      sortValue: (row) => row.formName.toLowerCase(),
    },
  ]);

  trackServiceLink = (row: { service: ServiceOffering }): string => row.service.id;

  closeEditor(): void {
    this.selected.set(null);
    this.draft.set(null);
  }

  openCreateModal(): void {
    this.name.set("");
    this.showCreate.set(true);
  }

  closeCreateModal(): void {
    this.showCreate.set(false);
  }

  async onTemplatePageChange(next: number): Promise<void> {
    if (this.busy() || next === this.page()) {
      return;
    }
    this.page.set(next);
    await this.loadTemplates();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.run(async () => {
        const result = await this.api.withAuthorizedClient(async (c) => ({
          rows: await c.intake.listTemplates(this.page(), this.abort.signal),
          services: await c.getTenantServices(),
        }));
        if (!this.alive) {
          return;
        }
        this.rows.set(result.rows);
        this.hasMoreTemplates.set(result.rows.length === TEMPLATE_PAGE_SIZE);
        this.services.set(result.services);
        await this.refreshServiceBindings();
      });
    } finally {
      if (this.alive) {
        this.loading.set(false);
      }
    }
  }

  /** Reload list rows without the busy guard — safe inside an existing `run()`. */
  private async reloadTemplateRows(): Promise<void> {
    const rows = await this.api.withAuthorizedClient((c) =>
      c.intake.listTemplates(this.page(), this.abort.signal),
    );
    if (this.alive) {
      this.rows.set(rows);
      this.hasMoreTemplates.set(rows.length === TEMPLATE_PAGE_SIZE);
    }
  }

  private async loadTemplates(): Promise<void> {
    await this.run(async () => {
      await this.reloadTemplateRows();
    });
  }

  private async refreshServiceBindings(): Promise<void> {
    const list = this.services();
    if (list.length === 0) {
      if (this.alive) {
        this.serviceBindings.set({});
      }
      return;
    }
    const entries = await this.api.withAuthorizedClient((c) =>
      Promise.all(
        list.map(async (service) => {
          const value = await c.intake.getBinding(service.id, this.abort.signal);
          return [service.id, value.templateId] as const;
        }),
      ),
    );
    if (this.alive) {
      this.serviceBindings.set(Object.fromEntries(entries));
    }
  }

  async run(fn: () => Promise<void>): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      await fn();
    } catch (e) {
      if (this.alive && !this.abort.signal.aborted) {
        this.error.set(this.apiErr(e, "intake.loadError"));
      }
    } finally {
      if (this.alive) {
        this.busy.set(false);
      }
    }
  }

  private accept(value: IntakeTemplate): void {
    if (!this.alive) {
      return;
    }
    this.selected.set(value);
    this.draft.set(value.draft ? structuredClone(value.draft) : null);
  }

  async open(id: string): Promise<void> {
    this.managementName = this.rows().find((x) => x.id === id)?.name ?? "";
    await this.run(async () => {
      this.accept(await this.api.withAuthorizedClient((c) => c.intake.getTemplate(id, this.abort.signal)));
    });
  }

  async create(): Promise<void> {
    const trimmed = this.name().trim();
    if (!trimmed) {
      return;
    }
    this.managementName = trimmed;
    await this.run(async () => {
      this.accept(await this.api.withAuthorizedClient((c) => c.intake.createTemplate(trimmed)));
      this.name.set("");
      this.showCreate.set(false);
      await this.reloadTemplateRows();
    });
  }

  async action(kind: "save" | "publish" | "clone" | "active"): Promise<void> {
    const selected = this.selected();
    if (!selected) {
      return;
    }
    await this.run(async () => {
      const value = await this.api.withAuthorizedClient(async (c) =>
        kind === "save"
          ? c.intake.saveDraft(selected.id, selected.revision, this.draft()!)
          : kind === "publish"
            ? c.intake.publish(
                selected.id,
                (await c.intake.saveDraft(selected.id, selected.revision, this.draft()!)).revision,
              )
            : kind === "clone"
              ? c.intake.cloneDraft(selected.id, selected.revision)
              : c.intake.updateTemplate(
                  selected.id,
                  this.managementName,
                  !selected.isActive,
                  selected.revision,
                ),
      );
      this.accept(value);
      if (this.alive) {
        await this.reloadTemplateRows();
        await this.refreshServiceBindings();
      }
    });
  }

  change(key: "title" | "description" | "instructions", value: string): void {
    this.draft.update((d) => (d ? { ...d, [key]: value } : d));
  }

  patchField(index: number, key: "label" | "helpText" | "required" | "type", value: string | boolean | number): void {
    this.draft.update((d) =>
      d
        ? {
            ...d,
            fields: d.fields.map((f, i) =>
              i === index ? { ...f, [key]: value, ...(key === "type" ? { choices: [] } : {}) } : f,
            ),
          }
        : d,
    );
  }

  add(): void {
    this.draft.update((d) =>
      d && d.fields.length < 30
        ? {
            ...d,
            fields: [
              ...d.fields,
              { id: crypto.randomUUID(), type: 0, label: "", helpText: null, required: false, choices: [] },
            ],
          }
        : d,
    );
  }

  remove(index: number): void {
    this.draft.update((d) => (d ? { ...d, fields: d.fields.filter((_, i) => i !== index) } : d));
  }

  move(index: number, delta: number): void {
    this.draft.update((d) => {
      if (!d || index + delta < 0 || index + delta >= d.fields.length) {
        return d;
      }
      const fields = [...d.fields];
      [fields[index], fields[index + delta]] = [fields[index + delta]!, fields[index]!];
      return { ...d, fields };
    });
  }

  addChoice(index: number): void {
    this.draft.update((d) =>
      d
        ? {
            ...d,
            fields: d.fields.map((f, i) =>
              i === index && f.choices.length < 20
                ? { ...f, choices: [...f.choices, { id: crypto.randomUUID(), label: "" }] }
                : f,
            ),
          }
        : d,
    );
  }

  choice(index: number, choice: number, label: string): void {
    this.draft.update((d) =>
      d
        ? {
            ...d,
            fields: d.fields.map((f, i) =>
              i === index ? { ...f, choices: f.choices.map((c, j) => (j === choice ? { ...c, label } : c)) } : f,
            ),
          }
        : d,
    );
  }

  removeChoice(index: number, choice: number): void {
    this.draft.update((d) =>
      d
        ? {
            ...d,
            fields: d.fields.map((f, i) =>
              i === index ? { ...f, choices: f.choices.filter((_, j) => j !== choice) } : f,
            ),
          }
        : d,
    );
  }

  async selectService(id: string): Promise<void> {
    this.serviceId.set(id);
    this.binding.set(null);
    if (!id) {
      return;
    }
    await this.run(async () => {
      const value = await this.api.withAuthorizedClient((c) => c.intake.getBinding(id, this.abort.signal));
      if (this.alive) {
        this.binding.set(value);
      }
    });
  }

  async bind(templateId: string): Promise<void> {
    const binding = this.binding();
    if (!binding) {
      return;
    }
    const service = this.serviceId();
    await this.run(async () => {
      const value = await this.api.withAuthorizedClient((c) =>
        c.intake.setBinding(service, templateId || null, binding.revision),
      );
      if (this.alive) {
        this.binding.set(value);
        this.serviceBindings.update((map) => ({ ...map, [service]: value.templateId }));
      }
    });
  }
}
