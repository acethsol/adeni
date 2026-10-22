import {
  Component,
  ElementRef,
  inject,
  input,
  output,
  signal,
} from "@angular/core";
import { AdeniCarbonIconComponent } from "./adeni-carbon-icon.component";
import { injectAdeniTranslate } from "./locale/adeni-i18n";

export type AdeniExportFormat = "csv" | "xlsx" | "pdf";

export type AdeniExportMenuItem = {
  format: AdeniExportFormat;
  label: string;
  disabled?: boolean;
  hint?: string;
};

@Component({
  selector: "adeni-export-menu",
  standalone: true,
  host: {
    "[attr.listExports]": '""',
  },
  imports: [AdeniCarbonIconComponent],
  template: `
    <div class="adeni-export-menu">
      <button
        type="button"
        class="adeni-export-menu-trigger"
        [attr.aria-expanded]="open()"
        aria-haspopup="menu"
        (click)="toggle($event)"
      >
        <adeni-carbon-icon name="download" [size]="16" aria-hidden="true" />
        <span>{{ label() }}</span>
        <adeni-carbon-icon name="chevron--down" [size]="16" aria-hidden="true" />
      </button>
      @if (open()) {
        <ul class="adeni-export-menu-panel" role="menu" [attr.aria-label]="label()">
          @for (item of items(); track item.format) {
            <li role="none">
              <button
                type="button"
                role="menuitem"
                class="adeni-export-menu-item"
                [disabled]="item.disabled"
                [title]="item.hint ?? ''"
                (click)="pick(item, $event)"
              >
                {{ item.label }}
              </button>
            </li>
          }
        </ul>
      }
    </div>
  `,
  styleUrl: "./styles/_data-grid.scss",
})
export class AdeniExportMenuComponent {
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly tr = injectAdeniTranslate();

  readonly label = input(this.tr("ui.ui.exportLabel"));
  readonly items = input<AdeniExportMenuItem[]>([
    { format: "csv", label: "CSV (.csv)", disabled: true },
    { format: "xlsx", label: "Excel (.xlsx)", disabled: true },
    { format: "pdf", label: "PDF (.pdf)", disabled: true },
  ]);

  readonly exportSelect = output<AdeniExportFormat>();

  readonly open = signal(false);

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    this.open.update((v) => !v);
    if (this.open()) {
      queueMicrotask(() => {
        document.addEventListener("click", this.onDocumentClick, { once: true });
      });
    }
  }

  pick(item: AdeniExportMenuItem, event: MouseEvent): void {
    event.stopPropagation();
    if (item.disabled) {
      return;
    }
    this.open.set(false);
    this.exportSelect.emit(item.format);
  }

  private readonly onDocumentClick = (event: MouseEvent): void => {
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  };
}
