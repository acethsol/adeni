import { calendarPermissionGuard } from "./pages/calendar-sync/calendar-permission.guard";
import { commercePermissionGuard } from "./pages/commerce/commerce-permission.guard";
import { Routes } from "@angular/router";
import { BusinessShellComponent } from "./layout/business-shell.component";
import { businessAccessGuard } from "./core/guards/business-access.guard";
import { businessAuthGuard } from "./core/guards/business-auth.guard";
import { businessPermissionGuard } from "./core/guards/business-permission.guard";
import { homeRedirectGuard } from "./core/guards/home-redirect.guard";
import { staffCalendarGuard } from "./core/guards/staff-calendar.guard";

export const routes: Routes = [
  { path: "setup", loadComponent: () => import("./pages/setup/setup.component").then((m) => m.SetupComponent), title: "Setup" },
  { path: "forbidden", loadComponent: () => import("./pages/forbidden/forbidden.component").then((m) => m.ForbiddenComponent), title: "Access denied" },
  { path: "accept-invite", loadComponent: () => import("./pages/accept-invite/accept-invite.component").then((m) => m.AcceptInviteComponent), title: "Accept invite" },
  {
    path: "kiosk",
    loadComponent: () => import("./pages/kiosk/kiosk.component").then((m) => m.KioskComponent),
    title: "Check-in",
    canActivate: [businessAccessGuard, businessAuthGuard, businessPermissionGuard],
  },
  {
    path: "",
    canActivate: [businessAccessGuard, businessAuthGuard],
    component: BusinessShellComponent,
    children: [
      {
        path: "",
        pathMatch: "full",
        canActivate: [homeRedirectGuard],
        loadComponent: () => import("./pages/dashboard/dashboard.component").then((m) => m.DashboardComponent),
      },
      { path: "settings/calendars", loadComponent: () => import("./pages/calendar-sync/calendar-sync.component").then(m => m.CalendarSyncComponent), canActivate: [calendarPermissionGuard], title: "Calendar sync" },
      { path: "public-page/booking-links", loadComponent: () => import("./pages/calendar-sync/booking-links.component").then(m => m.BookingLinksComponent), canActivate: [businessPermissionGuard], data: { permission: "business.public_page" }, title: "Book with Adeni" },
      { path: "bookings/:id/intake", loadComponent: () => import("./pages/intake/intake-review.component").then(m => m.IntakeReviewComponent), canActivate: [businessPermissionGuard], title: "Consultation forms" },
      { path: "finance/quick-pay", loadComponent: () => import("./pages/commerce/commerce.component").then(m => m.CommerceComponent), canActivate: [commercePermissionGuard], title: "Quick Pay" },
      { path: "services/intake", loadComponent: () => import("./pages/intake/intake-manager.component").then(m => m.IntakeManagerComponent), canActivate: [businessPermissionGuard], title: "Consultation forms" },
      { path: "overview", redirectTo: "dashboard", pathMatch: "full" },
      {
        path: "dashboard",
        loadComponent: () => import("./pages/dashboard/dashboard.component").then((m) => m.DashboardComponent),
        title: "Dashboard",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "bookings",
        loadComponent: () => import("./pages/bookings/bookings.component").then((m) => m.BookingsComponent),
        title: "Bookings",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "customers",
        loadComponent: () => import("./pages/customers/customers.component").then((m) => m.CustomersComponent),
        title: "Customers",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "retention",
        loadComponent: () => import("./pages/retention/retention-shell.component").then((m) => m.RetentionShellComponent),
        canActivate: [businessPermissionGuard],
        children: [
          { path: "", redirectTo: "packages", pathMatch: "full" },
          {
            path: "packages",
            loadComponent: () =>
              import("./pages/retention/retention-packages.component").then((m) => m.RetentionPackagesComponent),
            title: "Packages",
          },
          {
            path: "memberships",
            loadComponent: () =>
              import("./pages/retention/retention-memberships.component").then((m) => m.RetentionMembershipsComponent),
            title: "Memberships",
          },
          {
            path: "loyalty",
            loadComponent: () =>
              import("./pages/retention/retention-loyalty.component").then((m) => m.RetentionLoyaltyComponent),
            title: "Loyalty",
          },
        ],
      },
      {
        path: "customers/:id",
        loadComponent: () => import("./pages/customers/customer-detail.component").then((m) => m.CustomerDetailComponent),
        title: "Customer",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "queue",
        loadComponent: () => import("./pages/queue/queue.component").then((m) => m.QueueComponent),
        title: "Queue",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "calendar",
        loadComponent: () => import("./pages/staff/staff-calendar.component").then((m) => m.StaffCalendarComponent),
        title: "Appointments",
        canActivate: [businessPermissionGuard],
        data: { teamCalendar: true },
      },
      {
        path: "my-calendar",
        loadComponent: () => import("./pages/staff/staff-calendar.component").then((m) => m.StaffCalendarComponent),
        title: "My calendar",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "quotes",
        loadComponent: () => import("./pages/quotes/quotes.component").then((m) => m.QuotesComponent),
        title: "Quotes",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "messages",
        loadComponent: () => import("./pages/messages/messages.component").then((m) => m.MessagesComponent),
        title: "Messages",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "services",
        loadComponent: () => import("./pages/services/services.component").then((m) => m.ServicesComponent),
        title: "Services",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "services/:id",
        loadComponent: () => import("./pages/services/service-detail.component").then((m) => m.ServiceDetailComponent),
        title: "Service",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "staff",
        loadComponent: () => import("./pages/staff/staff.component").then((m) => m.StaffComponent),
        title: "Staff",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "staff/:id/calendar",
        loadComponent: () => import("./pages/staff/staff-calendar.component").then((m) => m.StaffCalendarComponent),
        title: "Staff calendar",
        canActivate: [staffCalendarGuard],
      },
      {
        path: "locations",
        loadComponent: () => import("./pages/locations/locations.component").then((m) => m.LocationsComponent),
        title: "Locations",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "availability",
        loadComponent: () => import("./pages/availability/availability.component").then((m) => m.AvailabilityComponent),
        title: "Hours",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "profile",
        loadComponent: () => import("./pages/profile/profile.component").then((m) => m.ProfileComponent),
        title: "Profile",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "public-page",
        loadComponent: () => import("./pages/public-page/public-page.component").then((m) => m.PublicPageSettingsComponent),
        title: "Public page",
        canActivate: [businessPermissionGuard],
      },
      { path: "register", loadComponent: () => import("./pages/register/register.component").then((m) => m.RegisterComponent), title: "Register" },
      {
        path: "finance",
        loadComponent: () => import("./pages/payments/payments.component").then((m) => m.PaymentsComponent),
        title: "Finance",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "settings",
        loadComponent: () => import("./pages/settings/settings.component").then((m) => m.SettingsComponent),
        title: "Settings",
        canActivate: [businessPermissionGuard],
      },
      {
        path: "settings/notifications",
        loadComponent: () => import("./pages/settings/settings.component").then((m) => m.SettingsComponent),
        title: "Notification settings",
        canActivate: [businessPermissionGuard],
        data: { settingsTab: "notifications" },
      },
      {
        path: "plan",
        loadComponent: () => import("./pages/plan/plan.component").then((m) => m.PlanComponent),
        title: "Plan",
        canActivate: [businessPermissionGuard],
      },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
