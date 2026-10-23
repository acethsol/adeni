import { Routes } from "@angular/router";
import { AdminShellComponent } from "./layout/portal-shell.component";
import { AdminDashboardComponent } from "./pages/dashboard/dashboard.component";
import { AdminPendingComponent } from "./pages/pending/pending.component";
import { AdminMarketsComponent } from "./pages/markets/markets.component";
import { AdminBusinessesComponent } from "./pages/businesses/businesses.component";
import { BusinessBillingComponent } from "./pages/businesses/business-billing.component";
import { BusinessCommercialAuditComponent } from "./pages/businesses/business-commercial-audit.component";
import { BusinessCommercialTermsComponent } from "./pages/businesses/business-commercial-terms.component";
import { BusinessDetailShellComponent } from "./pages/businesses/business-detail-shell.component";
import { BusinessOverviewComponent } from "./pages/businesses/business-overview.component";
import { AdminCustomersComponent } from "./pages/customers/customers.component";
import { AdminAuditLogsComponent } from "./pages/audit-logs/audit-logs.component";
import { AdminTrustBadgesComponent } from "./pages/trust-badges/trust-badges.component";
import { AdminReviewsComponent } from "./pages/reviews/reviews.component";
import { AdminInsightsComponent } from "./pages/insights/insights.component";
import { AdminTransactionsComponent } from "./pages/transactions/transactions.component";
import { AdminPricingCatalogComponent } from "./pages/pricing/pricing-catalog.component";
import { AdminPricingGmvComponent } from "./pages/pricing/pricing-gmv.component";
import { AdminPricingEntitlementsComponent } from "./pages/pricing/pricing-entitlements.component";
import { SetupComponent } from "./pages/setup/setup.component";
import { ForbiddenComponent } from "./pages/forbidden/forbidden.component";
import { adminAccessGuard } from "./core/guards/admin-access.guard";
import { adminAuthGuard } from "./core/guards/admin-auth.guard";

export const routes: Routes = [
  { path: "setup", component: SetupComponent, title: "Setup" },
  { path: "forbidden", component: ForbiddenComponent, title: "Access denied" },
  {
    path: "",
    canActivate: [adminAccessGuard, adminAuthGuard],
    component: AdminShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "dashboard", component: AdminDashboardComponent, title: "Dashboard" },
      { path: "pending", component: AdminPendingComponent, title: "Pending verifications" },
      { path: "trust-badges", component: AdminTrustBadgesComponent, title: "Trust badges" },
      { path: "reviews", component: AdminReviewsComponent, title: "Reviews" },
      { path: "markets", component: AdminMarketsComponent, title: "Markets" },
      { path: "pricing/plans", pathMatch: "full", redirectTo: "pricing/catalog" },
      {
        path: "pricing/catalog",
        component: AdminPricingCatalogComponent,
        title: "Plans & pricing",
      },
      {
        path: "pricing/gmv",
        component: AdminPricingGmvComponent,
        title: "GMV fees",
      },
      {
        path: "pricing/entitlements",
        component: AdminPricingEntitlementsComponent,
        title: "Plan entitlements",
      },
      { path: "insights", component: AdminInsightsComponent, title: "Subscription insights" },
      { path: "transactions", component: AdminTransactionsComponent, title: "Transactions" },
      { path: "businesses", component: AdminBusinessesComponent, title: "Businesses" },
      {
        path: "businesses/:tenantId",
        component: BusinessDetailShellComponent,
        children: [
          { path: "", pathMatch: "full", redirectTo: "overview" },
          {
            path: "overview",
            component: BusinessOverviewComponent,
            title: "Business overview",
          },
          {
            path: "terms",
            component: BusinessCommercialTermsComponent,
            title: "Commercial terms",
          },
          {
            path: "billing",
            component: BusinessBillingComponent,
            title: "Billing history",
          },
          {
            path: "audit",
            component: BusinessCommercialAuditComponent,
            title: "Commercial audit",
          },
        ],
      },
      { path: "customers", component: AdminCustomersComponent, title: "Customer privacy" },
      { path: "audit-logs", component: AdminAuditLogsComponent, title: "Audit trail" },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
