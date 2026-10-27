import { Routes } from "@angular/router";
import { AdminShellComponent } from "./layout/portal-shell.component";
import { adminAccessGuard } from "./core/guards/admin-access.guard";
import { adminAuthGuard } from "./core/guards/admin-auth.guard";

export const routes: Routes = [
  { path: "setup", loadComponent: () => import("./pages/setup/setup.component").then((m) => m.SetupComponent), title: "Setup" },
  { path: "forbidden", loadComponent: () => import("./pages/forbidden/forbidden.component").then((m) => m.ForbiddenComponent), title: "Access denied" },
  {
    path: "",
    canActivate: [adminAccessGuard, adminAuthGuard],
    component: AdminShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "dashboard", loadComponent: () => import("./pages/dashboard/dashboard.component").then((m) => m.AdminDashboardComponent), title: "Dashboard" },
      { path: "pending", loadComponent: () => import("./pages/pending/pending.component").then((m) => m.AdminPendingComponent), title: "Pending verifications" },
      { path: "trust-badges", loadComponent: () => import("./pages/trust-badges/trust-badges.component").then((m) => m.AdminTrustBadgesComponent), title: "Trust badges" },
      { path: "reviews", loadComponent: () => import("./pages/reviews/reviews.component").then((m) => m.AdminReviewsComponent), title: "Reviews" },
      { path: "markets", loadComponent: () => import("./pages/markets/markets.component").then((m) => m.AdminMarketsComponent), title: "Markets" },
      { path: "pricing/plans", pathMatch: "full", redirectTo: "pricing/catalog" },
      {
        path: "pricing/catalog",
        loadComponent: () => import("./pages/pricing/pricing-catalog.component").then((m) => m.AdminPricingCatalogComponent),
        title: "Plans & pricing",
      },
      {
        path: "pricing/gmv",
        loadComponent: () => import("./pages/pricing/pricing-gmv.component").then((m) => m.AdminPricingGmvComponent),
        title: "GMV fees",
      },
      {
        path: "pricing/entitlements",
        loadComponent: () => import("./pages/pricing/pricing-entitlements.component").then((m) => m.AdminPricingEntitlementsComponent),
        title: "Plan entitlements",
      },
      { path: "insights", loadComponent: () => import("./pages/insights/insights.component").then((m) => m.AdminInsightsComponent), title: "Subscription insights" },
      { path: "transactions", loadComponent: () => import("./pages/transactions/transactions.component").then((m) => m.AdminTransactionsComponent), title: "Transactions" },
      { path: "businesses", loadComponent: () => import("./pages/businesses/businesses.component").then((m) => m.AdminBusinessesComponent), title: "Businesses" },
      {
        path: "businesses/:tenantId",
        loadComponent: () => import("./pages/businesses/business-detail-shell.component").then((m) => m.BusinessDetailShellComponent),
        children: [
          { path: "", pathMatch: "full", redirectTo: "overview" },
          {
            path: "overview",
            loadComponent: () => import("./pages/businesses/business-overview.component").then((m) => m.BusinessOverviewComponent),
            title: "Business overview",
          },
          {
            path: "terms",
            loadComponent: () => import("./pages/businesses/business-commercial-terms.component").then((m) => m.BusinessCommercialTermsComponent),
            title: "Commercial terms",
          },
          {
            path: "billing",
            loadComponent: () => import("./pages/businesses/business-billing.component").then((m) => m.BusinessBillingComponent),
            title: "Billing history",
          },
          {
            path: "audit",
            loadComponent: () => import("./pages/businesses/business-commercial-audit.component").then((m) => m.BusinessCommercialAuditComponent),
            title: "Commercial audit",
          },
        ],
      },
      { path: "customers", loadComponent: () => import("./pages/customers/customers.component").then((m) => m.AdminCustomersComponent), title: "Customer privacy" },
      { path: "audit-logs", loadComponent: () => import("./pages/audit-logs/audit-logs.component").then((m) => m.AdminAuditLogsComponent), title: "Audit trail" },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
