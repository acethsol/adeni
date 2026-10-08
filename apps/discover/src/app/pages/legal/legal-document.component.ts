import { Component, computed, inject } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { t } from "@adeni/shared";
import { AdeniLocaleService } from "@adeni/ui";
import { map } from "rxjs";

export type LegalDocKind = "privacy" | "terms";

@Component({
  selector: "app-legal-document",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./legal-document.component.html",
  styleUrl: "./legal-document.component.scss",
})
export class LegalDocumentComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly localeService = inject(AdeniLocaleService);

  private readonly kind = toSignal(
    this.route.data.pipe(map((data) => (data["doc"] as LegalDocKind) ?? "privacy")),
    { initialValue: "privacy" as LegalDocKind },
  );

  readonly locale = this.localeService.locale;

  readonly title = computed(() => this.label(`legal.${this.kind()}.title`));
  readonly updated = computed(() => this.label(`legal.${this.kind()}.updated`));
  readonly intro = computed(() => this.label(`legal.${this.kind()}.intro`));
  readonly notice = computed(() => this.label(`legal.${this.kind()}.notice`));
  readonly sections = computed(() => {
    const doc = this.kind();
    const keys = ["s1", "s2", "s3", "s4", "s5", "s6"] as const;
    return keys.map((key) => ({
      heading: this.label(`legal.${doc}.sections.${key}.heading`),
      body: this.label(`legal.${doc}.sections.${key}.body`),
    }));
  });

  readonly otherDocPath = computed(() => (this.kind() === "privacy" ? "/terms" : "/privacy"));
  readonly otherDocLabel = computed(() =>
    this.label(this.kind() === "privacy" ? "footer.links.terms" : "footer.links.privacy"),
  );

  label(key: string, params?: Record<string, string | number>): string {
    return t(this.locale(), key, params);
  }
}
