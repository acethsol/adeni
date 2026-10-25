import { Routes } from "@angular/router";
import { PublicShellComponent } from "./layout/public-shell.component";
import { HomeComponent } from "./pages/home/home.component";
import { DiscoverComponent } from "./pages/discover/discover.component";
import { BusinessProfileComponent } from "./pages/business/business.component";
import { MyBookingsComponent } from "./pages/my-bookings/my-bookings.component";
import { MyMessagesComponent } from "./pages/my-messages/my-messages.component";
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
  { path: "my-gift-cards", canActivate: [customerAuthGuard], loadComponent: () => import("./pages/pay/purchased-gifts.component").then(m => m.PurchasedGiftsComponent), title: "Purchased gift cards" },
  {
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
      { path: "my-bookings/:bookingId/intake/:assignmentId", canActivate: [customerAuthGuard], loadComponent: () => import("./pages/intake/intake.component").then(m => m.IntakeComponent), title: "Consultation forms" },
      { path: "my-bookings/:bookingId/intake", canActivate: [customerAuthGuard], loadComponent: () => import("./pages/intake/intake.component").then(m => m.IntakeComponent), title: "Consultation forms" },
      {
        path: "my-bookings",
        canActivate: [customerAuthGuard],
        component: MyBookingsComponent,
        title: "My bookings",
      },
      {
        path: "my-messages",
        canActivate: [customerAuthGuard],
        component: MyMessagesComponent,
        title: "Messages",
      },
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
