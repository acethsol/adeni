import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";
import { FormsModule } from "@angular/forms";
import { map } from "rxjs";
import {
  formatSlotTime,
  hasTenantEntitlement,
  pageRangeLabel,
  totalPages as computeTotalPages,
  type BusinessProfile,
  type MessageTemplate,
  type MessageThreadDetail,
  type MessageThreadSummary,
} from "@adeni/shared";
import { AdeniPortalEmptyStateComponent, PortalPageComponent } from "@adeni/ui";
import { filterPortalTabs } from "../../core/portal-access";
import { findPortalNavItem } from "../../core/portal-nav";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

@Component({
  selector: "app-messages",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, PortalTabsComponent, AdeniPortalEmptyStateComponent],
  templateUrl: "./messages.component.html",
  styleUrl: "./messages.component.scss",
})
export class MessagesComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "whatsapp")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "whatsapp",
  });

  private readonly navTabs = findPortalNavItem("/messages")?.item.tabs ?? [
    { id: "inbox", label: "Inbox", entitlement: "messaging" as const },
    { id: "whatsapp", label: "WhatsApp" },
  ];

  readonly profile = signal<BusinessProfile | null>(null);
  readonly visibleTabs = computed(() =>
    filterPortalTabs(this.navTabs, this.profile()?.entitlements ?? null),
  );
  readonly messagingEntitled = computed(() =>
    hasTenantEntitlement(this.profile()?.entitlements, "messaging"),
  );

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly threads = signal<MessageThreadSummary[]>([]);
  readonly threadTotalCount = signal(0);
  readonly threadPage = signal(1);
  readonly threadPageSize = 25;
  readonly threadTotalPageCount = computed(() =>
    computeTotalPages(this.threadTotalCount(), this.threadPageSize),
  );
  readonly threadPageLabel = computed(() =>
    pageRangeLabel(this.threadPage(), this.threadPageSize, this.threadTotalCount()),
  );
  readonly templates = signal<MessageTemplate[]>([]);
  readonly detail = signal<MessageThreadDetail | null>(null);
  readonly sending = signal(false);
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
        let threadTotalCount = 0;
        let threadPage = 1;
        if (hasTenantEntitlement(profile.entitlements, "messaging")) {
          const [threadPageResult, templateList] = await Promise.all([
            client.listTenantMessageThreads({
              page: this.threadPage(),
              pageSize: this.threadPageSize,
            }),
            client.getTenantMessageTemplates(),
          ]);
          threads = threadPageResult.items;
          threadTotalCount = threadPageResult.totalCount;
          threadPage = threadPageResult.page;
          templates = templateList;
        }
        return { threads, templates, profile, threadTotalCount, threadPage };
      });
      this.threads.set(snapshot.threads);
      this.threadTotalCount.set(snapshot.threadTotalCount);
      this.threadPage.set(snapshot.threadPage);
      this.templates.set(snapshot.templates);
      this.profile.set(snapshot.profile);
      if (!this.route.snapshot.queryParamMap.get("tab")) {
        const defaultTab = hasTenantEntitlement(snapshot.profile.entitlements, "messaging")
          ? "inbox"
          : "whatsapp";
        await this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { tab: defaultTab },
          replaceUrl: true,
        });
      }
    } catch {
      this.error.set("Could not load messages.");
    } finally {
      this.loading.set(false);
    }
  }

  canNextThreadPage(): boolean {
    return this.threadPage() < this.threadTotalPageCount();
  }

  prevThreadPage(): void {
    if (this.threadPage() <= 1) {
      return;
    }
    this.threadPage.update((p) => p - 1);
    this.detail.set(null);
    void this.load();
  }

  nextThreadPage(): void {
    if (!this.canNextThreadPage()) {
      return;
    }
    this.threadPage.update((p) => p + 1);
    this.detail.set(null);
    void this.load();
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
