import { Routes } from "@angular/router";
import { PublicShellComponent } from "./layout/public-shell.component";
import { HomeComponent } from "./pages/home/home.component";
import { DiscoverComponent } from "./pages/discover/discover.component";
import { BusinessProfileComponent } from "./pages/business/business.component";
import { MyBookingsComponent } from "./pages/my-bookings/my-bookings.component";
import { LegalDocumentComponent } from "./pages/legal/legal-document.component";
import { PublicDocumentComponent } from "./pages/document/document.component";
import { QueueJoinComponent } from "./pages/queue-join/queue-join.component";
import { CheckoutStubComponent } from "./pages/checkout-stub/checkout-stub.component";
import { customerAuthGuard } from "./core/guards/customer-auth.guard";

export const routes: Routes = [
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
      {
        path: "my-bookings",
        canActivate: [customerAuthGuard],
        component: MyBookingsComponent,
        title: "My bookings",
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
    ],
  },
  { path: "**", redirectTo: "" },
];
