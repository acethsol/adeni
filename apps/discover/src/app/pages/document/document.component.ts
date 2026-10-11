import { DatePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute } from "@angular/router";
import type { BillingDocumentResponse } from "@adeni/shared";
import { formatPrice } from "@adeni/shared";
import { AdeniApiClient } from "@adeni/api-client";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-public-document",
  standalone: true,
  imports: [DatePipe],
  templateUrl: "./document.component.html",
  styleUrl: "./document.component.scss",
})
export class PublicDocumentComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly document = signal<BillingDocumentResponse | null>(null);
  readonly formatPrice = formatPrice;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const token = this.route.snapshot.paramMap.get("token");
    if (!token) {
      this.error.set("Document not found.");
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
      const doc = await client.getPublicBillingDocument(token);
      this.document.set(doc);
    } catch {
      this.error.set("This document is unavailable or has been voided.");
      this.document.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  kindLabel(kind: string): string {
    return kind === "invoice" ? "Invoice" : "Receipt";
  }

  pdfUrl(token: string): string {
    return `${this.config.apiBaseUrl.replace(/\/$/, "")}/api/v1/public/documents/${encodeURIComponent(token)}/pdf`;
  }
}
