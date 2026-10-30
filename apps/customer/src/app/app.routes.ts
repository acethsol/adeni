import { Routes } from "@angular/router";
import { PublicShellComponent } from "./layout/public-shell.component";
import { CustomerAppShellComponent } from "./app-surface/shell/customer-app-shell.component";
import { HomeComponent } from "./pages/home/home.component";
import { DiscoverComponent } from "./pages/discover/discover.component";
import { BusinessProfileComponent } from "./pages/business/business.component";
import { LegalDocumentComponent } from "./pages/legal/legal-document.component";
import { PublicDocumentComponent } from "./pages/document/document.component";
import { QueueJoinComponent } from "./pages/queue-join/queue-join.component";
import { CheckoutStubComponent } from "./pages/checkout-stub/checkout-stub.component";
import { PlanPageComponent } from "./pages/plan/plan.component";
import { SeoMarketPageComponent } from "./pages/seo-market/seo-market.component";
import { customerAuthGuard } from "./core/guards/customer-auth.guard";
import { seoMarketCanMatch } from "./core/seo-market.guard";

export const routes: Routes = [
  { path: "pay", loadComponent: () => import("./pages/pay/pay.component").then(m => m.PayComponent), title: "Payment" },
  {
    // Authenticated customer app surface — client-rendered, noindex (app.routes.server.ts). Declared before the
    // public group so no market/SEO catch-all can ever shadow it.
    path: "app",
    component: CustomerAppShellComponent,
    canActivate: [customerAuthGuard],
    data: { surface: "app" },
    children: [
      { path: "", pathMatch: "full", redirectTo: "home" },
      {
        path: "home",
        loadComponent: () => import("./app-surface/pages/home/customer-home.component").then(m => m.CustomerHomeComponent),
        title: "Home",
      },
      {
        path: "discover",
        loadComponent: () => import("./app-surface/pages/discover/app-discover.component").then(m => m.AppDiscoverComponent),
        title: "Discover",
      },
      {
        path: "bookings/:bookingId/intake/:assignmentId",
        loadComponent: () => import("./pages/intake/intake.component").then(m => m.IntakeComponent),
        title: "Consultation forms",
      },
      {
        path: "bookings/:bookingId/intake",
        loadComponent: () => import("./pages/intake/intake.component").then(m => m.IntakeComponent),
        title: "Consultation forms",
      },
      {
        path: "bookings/:bookingId",
        loadComponent: () => import("./app-surface/pages/booking-detail/booking-detail.component").then(m => m.BookingDetailComponent),
        title: "Booking",
      },
      {
        path: "bookings",
        loadComponent: () =>
          import("./app-surface/pages/bookings/my-bookings.component").then(m => m.MyBookingsComponent),
        title: "My bookings",
      },
      {
        path: "messages",
        loadComponent: () =>
          import("./app-surface/pages/messages/my-messages.component").then(m => m.MyMessagesComponent),
        title: "Messages",
      },
      {
        path: "benefits",
        loadComponent: () => import("./app-surface/pages/benefits/benefits-hub.component").then(m => m.BenefitsHubComponent),
        children: [
          { path: "", pathMatch: "full", redirectTo: "packages" },
          {
            path: "packages",
            loadComponent: () =>
              import("./app-surface/pages/benefits/packages/my-packages.component").then(m => m.MyPackagesComponent),
            title: "My packages",
          },
          {
            path: "memberships",
            loadComponent: () =>
              import("./app-surface/pages/benefits/memberships/my-memberships.component").then(
                m => m.MyMembershipsComponent,
              ),
            title: "My memberships",
          },
          {
            path: "gift-cards",
            loadComponent: () =>
              import("./app-surface/pages/benefits/gift-cards/purchased-gifts.component").then(
                m => m.PurchasedGiftsComponent,
              ),
            title: "Purchased gift cards",
          },
        ],
      },
      {
        path: "profile",
        loadComponent: () => import("./app-surface/pages/profile/customer-profile.component").then(m => m.CustomerProfileComponent),
        title: "Profile",
      },
      { path: "**", redirectTo: "home" },
    ],
  },  {
    path: "checkout/stub/:reference",
    component: CheckoutStubComponent,
    title: "Checkout",
  },
  {
    path: "",
    component: PublicShellComponent,
    children: [
      { path: "", pathMatch: "full", component: HomeComponent },
      { path: "discover", component: DiscoverComponent, title: "Explore" },
      { path: "businesses/:slug", component: BusinessProfileComponent },
      {
        path: "documents/:token",
        component: PublicDocumentComponent,
        title: "Document",
      },
      {
        path: "q/:slug",
        component: QueueJoinComponent,
        title: "Join queue",
      },
      {
        path: "privacy",
        component: LegalDocumentComponent,
        data: { doc: "privacy" },
        title: "Privacy",
      },
      {
        path: "terms",
        component: LegalDocumentComponent,
        data: { doc: "terms" },
        title: "Terms",
      },
      {
        path: "plan",
        component: PlanPageComponent,
        title: "Plans",
      },
      {
        path: ":marketId",
        canMatch: [seoMarketCanMatch],
        component: SeoMarketPageComponent,
      },
      {
        path: ":marketId/:segment",
        canMatch: [seoMarketCanMatch],
        component: SeoMarketPageComponent,
      },
      {
        path: ":marketId/:areaSlug/:categorySlug",
        canMatch: [seoMarketCanMatch],
        component: SeoMarketPageComponent,
      },
    ],
  },
  { path: "**", redirectTo: "" },
];
