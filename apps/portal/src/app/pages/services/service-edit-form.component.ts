import { Component, input, output } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { ServiceMenuGroup, ServiceTemplate } from "@adeni/shared";
import { getCategoryLabel } from "@adeni/shared";
import { AdeniStaffButtonComponent, injectAdeniTranslate } from "@adeni/ui";
import type { ServiceDraft } from "./service-draft";

@Component({
  selector: "app-service-edit-form",
  standalone: true,
  imports: [FormsModule, AdeniStaffButtonComponent],
  templateUrl: "./service-edit-form.component.html",
  styleUrl: "./service-edit-form.component.scss",
})
export class ServiceEditFormComponent {
  protected readonly tr = injectAdeniTranslate();

  readonly draft = input.required<ServiceDraft>();
  readonly groups = input.required<ServiceMenuGroup[]>();
  readonly catalogTemplates = input<ServiceTemplate[]>([]);
  readonly catalogLoading = input(false);
  readonly disabled = input(false);
  readonly showActions = input(true);

  readonly cancel = output<void>();
  readonly save = output<void>();
  readonly applyTemplate = output<ServiceTemplate>();

  readonly categoryLabel = getCategoryLabel;
}
