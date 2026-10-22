import { Component, input, output, signal, effect } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { AdminMarket } from "@adeni/shared";
import { AdeniModalComponent, AdeniStaffButtonComponent, injectAdeniTranslate } from "@adeni/ui";

export type MarketFormMode = "create" | "edit";

export type MarketFormValue = {
  id: string;
  name: string;
  countryCode: string;
  currency: string;
  timeZoneId: string;
  defaultLat: number;
  defaultLng: number;
  languagesEn: boolean;
  languagesFr: boolean;
  isLive: boolean;
  launchNote: string;
};

const DEFAULT_FORM: MarketFormValue = {
  id: "",
  name: "",
  countryCode: "NG",
  currency: "NGN",
  timeZoneId: "Africa/Lagos",
  defaultLat: 6.5244,
  defaultLng: 3.3792,
  languagesEn: true,
  languagesFr: false,
  isLive: false,
  launchNote: "",
};

@Component({
  selector: "app-market-form-modal",
  standalone: true,
  imports: [FormsModule, AdeniModalComponent, AdeniStaffButtonComponent],
  templateUrl: "./market-form-modal.component.html",
  styleUrl: "./market-form-modal.component.scss",
})
export class MarketFormModalComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly open = input(false);
  readonly mode = input<MarketFormMode>("create");
  readonly market = input<AdminMarket | null>(null);
  readonly saving = input(false);

  readonly closed = output<void>();
  readonly save = output<MarketFormValue>();

  readonly form = signal<MarketFormValue>({ ...DEFAULT_FORM });

  readonly isCreate = () => this.mode() === "create";

  constructor() {
    effect(() => {
      if (!this.open()) {
        return;
      }
      const m = this.market();
      if (this.mode() === "edit" && m) {
        this.form.set({
          id: m.id,
          name: m.name,
          countryCode: m.countryCode,
          currency: m.currency,
          timeZoneId: m.timeZoneId,
          defaultLat: m.defaultLat,
          defaultLng: m.defaultLng,
          languagesEn: m.languages.includes("en"),
          languagesFr: m.languages.includes("fr"),
          isLive: m.isLive,
          launchNote: m.launchNote ?? "",
        });
      } else {
        this.form.set({ ...DEFAULT_FORM });
      }
    });
  }

  patch(partial: Partial<MarketFormValue>): void {
    this.form.update((current) => ({ ...current, ...partial }));
  }

  onSubmit(): void {
    this.save.emit(this.form());
  }

  onClose(): void {
    this.closed.emit();
  }
}
