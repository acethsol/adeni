import { Component, input, model } from "@angular/core";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";

export type AdeniFilterSelectOption = {
  value: string;
  label: string;
};

@Component({
  selector: "adeni-filter-select",
  standalone: true,
  imports: [AdeniCarbonIconComponent],
  template: `
    <div class="adeni-filter-select" [class.adeni-filter-select--toolbar]="layout() === 'toolbar'">
      <label [for]="fieldId" [class.visually-hidden]="layout() === 'toolbar'">{{ label() }}</label>
      <div class="adeni-filter-select-field">
        <select
          [id]="fieldId"
          [value]="value()"
          (change)="onChange($any($event.target).value)"
        >
          @for (option of options(); track option.value) {
            <option [value]="option.value">{{ option.label }}</option>
          }
        </select>
        <adeni-carbon-icon
          class="adeni-filter-select-chevron"
          name="chevron--down"
          [size]="16"
          aria-hidden="true"
        />
      </div>
    </div>
  `,
  styleUrl: "./styles/_data-grid-filter-select.scss",
})
export class AdeniFilterSelectComponent {
  readonly fieldId = `adeni-filter-${Math.random().toString(36).slice(2, 9)}`;

  readonly label = input.required<string>();
  readonly options = input.required<AdeniFilterSelectOption[]>();
  readonly layout = input<"default" | "toolbar">("default");
  readonly value = model<string>("all");

  onChange(next: string): void {
    this.value.set(next);
  }
}
