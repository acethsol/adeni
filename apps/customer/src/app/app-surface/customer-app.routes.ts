import { Routes } from "@angular/router";

/** Authenticated `/app/*` tree — lazy-loaded from root `app.routes.ts` so public bundles stay smaller. */
export const CUSTOMER_APP_ROUTES: Routes = [
  {
    path: "",
    loadComponent: () =>
      import("./shell/customer-portal-shell.component").then((m) => m.CustomerPortalShellComponent),
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "home", pathMatch: "full", redirectTo: "dashboard" },
      {
        path: "dashboard",
        loadComponent: () =>
          import("./pages/dashboard/customer-dashboard.component").then((m) => m.CustomerDashboardComponent),
        title: "Dashboard",
      },
      {
        path: "calendar",
        loadComponent: () =>
          import("./pages/calendar/customer-calendar.component").then((m) => m.CustomerCalendarComponent),
        title: "Calendar",
      },
      {
        path: "discover",
        loadComponent: () => import("./pages/discover/app-discover.component").then((m) => m.AppDiscoverComponent),
        title: "Discover",
      },
      {
        path: "bookings/:bookingId/intake/:assignmentId",
        loadComponent: () => import("../pages/intake/intake.component").then((m) => m.IntakeComponent),
        title: "Consultation forms",
      },
      {
        path: "bookings/:bookingId/intake",
        loadComponent: () => import("../pages/intake/intake.component").then((m) => m.IntakeComponent),
        title: "Consultation forms",
      },
      {
        path: "bookings/:bookingId",
        loadComponent: () =>
          import("./pages/booking-detail/booking-detail.component").then((m) => m.BookingDetailComponent),
        title: "Booking",
      },
      {
        path: "bookings",
        loadComponent: () => import("./pages/bookings/bookings.component").then((m) => m.BookingsComponent),
        title: "Bookings",
      },
      {
        path: "messages",
        loadComponent: () => import("./pages/messages/my-messages.component").then((m) => m.MyMessagesComponent),
        title: "Messages",
      },
      {
        path: "benefits",
        loadComponent: () => import("./pages/benefits/benefits-hub.component").then((m) => m.BenefitsHubComponent),
        children: [
          { path: "", pathMatch: "full", redirectTo: "packages" },
          {
            path: "packages",
            loadComponent: () =>
              import("./pages/benefits/packages/my-packages.component").then((m) => m.MyPackagesComponent),
            title: "My packages",
          },
          {
            path: "memberships",
            loadComponent: () =>
              import("./pages/benefits/memberships/my-memberships.component").then(
                (m) => m.MyMembershipsComponent,
              ),
            title: "My memberships",
          },
          {
            path: "gift-cards",
            loadComponent: () =>
              import("./pages/benefits/gift-cards/purchased-gifts.component").then(
                (m) => m.PurchasedGiftsComponent,
              ),
            title: "Purchased gift cards",
          },
        ],
      },
      {
        path: "receipts",
        loadComponent: () => import("./pages/receipts/my-receipts.component").then((m) => m.MyReceiptsComponent),
        title: "Receipts",
      },
      {
        path: "profile",
        loadComponent: () => import("./pages/profile/customer-profile.component").then((m) => m.CustomerProfileComponent),
        title: "Profile",
      },
      { path: "**", redirectTo: "dashboard" },
    ],
  },
];
