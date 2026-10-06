import { Component, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { map } from "rxjs";
import {
  formatSlotTime,
  type BusinessProfile,
  type MessageTemplate,
  type MessageThreadDetail,
  type MessageThreadSummary,
} from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

@Component({
  selector: "app-messages",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, RouterLink, PortalTabsComponent],
  templateUrl: "./messages.component.html",
  styleUrl: "./messages.component.scss",
})
export class MessagesComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly route = inject(ActivatedRoute);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "inbox")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "inbox",
  });
  readonly tabs = [
    { id: "inbox", label: "Inbox" },
    { id: "whatsapp", label: "WhatsApp" },
  ];

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly threads = signal<MessageThreadSummary[]>([]);
  readonly templates = signal<MessageTemplate[]>([]);
  readonly detail = signal<MessageThreadDetail | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly sending = signal(false);
  readonly messagingLocked = signal(false);
  draft = "";

  readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const snapshot = await this.api.withAuthorizedClient(async (client) => {
        const profile = await client.getTenantProfile();
        let threads: MessageThreadSummary[] = [];
        let templates: MessageTemplate[] = [];
        let locked = false;
        try {
          [threads, templates] = await Promise.all([
            client.listTenantMessageThreads(),
            client.getTenantMessageTemplates(),
          ]);
        } catch {
          locked = true;
        }
        return { threads, templates, profile, locked };
      });
      this.threads.set(snapshot.threads);
      this.templates.set(snapshot.templates);
      this.profile.set(snapshot.profile);
      this.messagingLocked.set(snapshot.locked);
    } catch {
      this.error.set("Could not load messages.");
    } finally {
      this.loading.set(false);
    }
  }

  async openThread(thread: MessageThreadSummary): Promise<void> {
    this.error.set(null);
    try {
      const detail = await this.api.withAuthorizedClient(async (client) => {
        const loaded = await client.getTenantMessageThread(thread.id);
        await client.markTenantThreadRead(thread.id);
        return loaded;
      });
      this.detail.set(detail);
      this.threads.update((items) =>
        items.map((item) => (item.id === thread.id ? { ...item, unreadCount: 0 } : item)),
      );
    } catch {
      this.error.set("Could not open that conversation.");
    }
  }

  async send(): Promise<void> {
    const thread = this.detail();
    const body = this.draft.trim();
    if (!thread || !body) return;
    this.sending.set(true);
    this.error.set(null);
    try {
      const message = await this.api.withAuthorizedClient((client) =>
        client.sendTenantMessage(thread.id, { body }),
      );
      this.detail.update((current) =>
        current ? { ...current, messages: [...current.messages, message] } : current,
      );
      this.draft = "";
    } catch {
      this.error.set("Could not send that message.");
    } finally {
      this.sending.set(false);
    }
  }

  useTemplate(template: MessageTemplate): void {
    this.draft = template.body;
  }

  primarySlug(): string | null {
    const profile = this.profile();
    if (!profile) return null;
    const primary = profile.locations.find((location) => location.isPrimary) ?? profile.locations[0];
    return primary?.slug ?? null;
  }

  whatsAppUrl(message: string): string {
    return `https://wa.me/?text=${encodeURIComponent(message)}`;
  }

  whatsAppNotes(): { title: string; body: string }[] {
    const name = this.profile()?.businessName ?? "us";
    const link = this.primarySlug()
      ? `${this.config.discoverWebUrl}/businesses/${this.primarySlug()}`
      : this.config.discoverWebUrl;
    return [
      { title: "Booking confirmed", body: `Hi! Your booking with ${name} is confirmed. ${link}` },
      { title: "Reminder", body: `Reminder from ${name}: we look forward to seeing you. ${link}` },
      { title: "Thank you", body: `Thanks for visiting ${name}. Book again anytime: ${link}` },
    ];
  }

  async copy(text: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(text);
      this.notice.set("Copied.");
    } catch {
      this.error.set("Could not copy.");
    }
  }
}
