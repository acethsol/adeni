import { inject, Injectable } from "@angular/core";
import { hasTenantEntitlement, layoutIncludesWidget, mergeTenantEntitlements, resolveDashboardCapabilities,
  resolveDashboardRange, type ConversationSummary, type DashboardRangePreset, type QuoteRequestResponse } from "@adeni/shared";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { PORTAL_ALL_LOCATIONS_SCOPE } from "../../core/services/portal-active-location-store";
import { resolveDashboardLayoutFromApi } from "./dashboard-layout-sync";

/** Page-scoped request orchestration; presentation signals and realtime subscriptions stay in the container. */
@Injectable()
export class DashboardDataService {
  private readonly businessApi = inject(BusinessApiService);
  private readonly activeLocation = inject(PortalActiveLocationService);

  async load(rangePreset: DashboardRangePreset, isPractitionerView: () => boolean) {
    const range = resolveDashboardRange(rangePreset);
    return this.businessApi.withAuthorizedClient(async (client) => {
      const [profile, exportEntitlements] = await Promise.all([
        client.getTenantProfile(),
        client.getTenantEntitlements().catch(() => null),
      ]);
      this.activeLocation.syncFromProfile(profile);
      const locationQueryId = this.activeLocation.tenantApiLocationId(
        mergeTenantEntitlements(exportEntitlements, null, profile.entitlements),
      );
      const storedLayout = await resolveDashboardLayoutFromApi(
        () => client.getTenantDashboardLayout(),
        (layout) => client.putTenantDashboardLayout(layout),
        profile.tenantId,
        isPractitionerView(),
      );
      const wantsServiceMix = layoutIncludesWidget(storedLayout, "service_mix");
      const wantsCollected = layoutIncludesWidget(storedLayout, "collected_revenue");
      const wantsBookedValue = layoutIncludesWidget(storedLayout, "booked_value");
      const wantsStaffBoard = layoutIncludesWidget(storedLayout, "staff_leaderboard");
      const wantsQueue = layoutIncludesWidget(storedLayout, "queue_now");
      const wantsQuotes = layoutIncludesWidget(storedLayout, "quote_pipeline");
      const wantsUnread = layoutIncludesWidget(storedLayout, "messages_unread");
      const analytics = hasTenantEntitlement(profile.entitlements ?? null, "advancedAnalytics");
      const messaging = hasTenantEntitlement(profile.entitlements ?? null, "messaging");
      const staffBoard = hasTenantEntitlement(profile.entitlements ?? null, "staffPerformanceAnalytics");
      const caps = resolveDashboardCapabilities(profile);

      const [
        bookings,
        services,
        usage,
        summary,
        series,
        collectedSeries,
        serviceBreakdown,
        bookedSeries,
        staffBreakdown,
        unreadCount,
        messageInbox,
        queueFloor,
        quotes,
      ] = await Promise.all([
        client.listTenantBookings({ tab: "pending", page: 1, pageSize: 100, locationId: locationQueryId }).then(
          (page) => page.items,
        ),
        client.getTenantServices(),
        client.getTenantSubscriptionUsage(locationQueryId).catch(() => null),
        client.getTenantDashboardSummary(range.from, range.to, locationQueryId),
        client.getTenantDashboardSeries("appointments", range.from, range.to, locationQueryId),
        analytics && wantsCollected
          ? client.getTenantDashboardSeries("collected", range.from, range.to, locationQueryId).catch(() => null)
          : Promise.resolve(null),
        analytics && wantsServiceMix
          ? client.getTenantDashboardBreakdown("service", range.from, range.to, locationQueryId).catch(() => null)
          : Promise.resolve(null),
        analytics && wantsBookedValue
          ? client.getTenantDashboardSeries("booked_value", range.from, range.to, locationQueryId).catch(() => null)
          : Promise.resolve(null),
        staffBoard && wantsStaffBoard
          ? client.getTenantDashboardBreakdown("staff", range.from, range.to, locationQueryId).catch(() => null)
          : Promise.resolve(null),
        wantsUnread && messaging
          ? client.getTenantMessageUnreadCount(locationQueryId).catch(() => 0)
          : Promise.resolve(0),
        wantsUnread && messaging
          ? client
              .listTenantConversations({ page: 1, pageSize: 4, locationId: locationQueryId })
              .catch(() => ({ items: [] as ConversationSummary[], totalCount: 0, page: 1, pageSize: 4 }))
          : Promise.resolve({ items: [] as ConversationSummary[], totalCount: 0, page: 1, pageSize: 4 }),
        wantsQueue && caps.includes("walk_in_queue")
          ? client.getTenantQueueFloor(locationQueryId).catch(() => null)
          : Promise.resolve(null),
        wantsQuotes && caps.includes("quotes")
          ? client
              .listTenantQuotes({ locationId: locationQueryId })
              .catch(() => [] as QuoteRequestResponse[])
          : Promise.resolve([] as QuoteRequestResponse[]),
      ]);

      const allBranches =
        this.activeLocation.selectedLocationId() === PORTAL_ALL_LOCATIONS_SCOPE;
      const location = allBranches
        ? null
        : this.activeLocation.resolveContextLocation(profile);
      let rating: { avg: number | null; count: number } | null = null;
      if (location) {
        try {
          const publicProfile = await client.getBusinessProfile(location.slug);
          rating = {
            avg: publicProfile.ratingAvg ?? null,
            count: publicProfile.reviewCount ?? 0,
          };
        } catch {
          rating = null;
        }
      }
      return {
        profile,
        exportEntitlements,
        storedLayout,
        bookings,
        services,
        usage,
        rating,
        summary,
        series,
        collectedSeries,
        serviceBreakdown,
        bookedSeries,
        staffBreakdown,
        unreadCount,
        messageInbox,
        queueFloor,
        quotes,
      };
    });
  }
}
