import { Component, inject, input, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { discoveryCtaLabel } from "@adeni/shared";
import { AdeniApiError } from "@adeni/api-client";
import { CustomerApiService } from "../core/services/customer-api.service";

@Component({
  selector: "app-quote-request-panel",
  standalone: true,
  imports: [FormsModule],
  templateUrl: "./quote-request-panel.component.html",
  styleUrl: "./quote-request-panel.component.scss",
})
export class QuoteRequestPanelComponent {
  private readonly api = inject(CustomerApiService);

  readonly slug = input.required<string>();
  readonly enabled = input.required<boolean>();

  readonly title = discoveryCtaLabel("get_quote");
  readonly description = signal("");
  readonly serviceAddress = signal("");
  readonly error = signal<string | null>(null);
  readonly submitted = signal(false);
  readonly submitting = signal(false);

  signIn(): void {
    void this.api.login(window.location.pathname);
  }

  async submit(): Promise<void> {
    if (!this.enabled()) {
      return;
    }

    this.submitting.set(true);
    this.error.set(null);

    try {
      await this.api.withAuthorizedClient((c) =>
        c.createQuoteRequest(this.slug(), {
          description: this.description().trim(),
          serviceAddress: this.serviceAddress().trim() || undefined,
        }),
      );
      this.submitted.set(true);
    } catch (err) {
      this.error.set(
        err instanceof AdeniApiError ? err.message : "Could not send quote request.",
      );
    } finally {
      this.submitting.set(false);
    }
  }
}
