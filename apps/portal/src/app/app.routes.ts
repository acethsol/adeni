import { Routes } from "@angular/router";
import { PortalShellComponent } from "./layout/portal-shell.component";
import { portalAccessGuard } from "./core/guards/portal-access.guard";
import { businessAuthGuard } from "./core/guards/business-auth.guard";
import { portalPermissionGuard } from "./core/guards/portal-permission.guard";
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
    canActivate: [portalAccessGuard, businessAuthGuard, portalPermissionGuard],
  },
  {
    path: "",
    canActivate: [portalAccessGuard, businessAuthGuard],
    component: PortalShellComponent,
    children: [
      {
        path: "",
        pathMatch: "full",
        canActivate: [homeRedirectGuard],
        loadComponent: () => import("./pages/dashboard/dashboard.component").then((m) => m.DashboardComponent),
      },
      { path: "overview", redirectTo: "dashboard", pathMatch: "full" },
      {
        path: "dashboard",
        loadComponent: () => import("./pages/dashboard/dashboard.component").then((m) => m.DashboardComponent),
        title: "Dashboard",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "bookings",
        loadComponent: () => import("./pages/bookings/bookings.component").then((m) => m.BookingsComponent),
        title: "Bookings",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "customers",
        loadComponent: () => import("./pages/customers/customers.component").then((m) => m.CustomersComponent),
        title: "Customers",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "customers/:id",
        loadComponent: () => import("./pages/customers/customer-detail.component").then((m) => m.CustomerDetailComponent),
        title: "Customer",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "queue",
        loadComponent: () => import("./pages/queue/queue.component").then((m) => m.QueueComponent),
        title: "Queue",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "calendar",
        loadComponent: () => import("./pages/staff/staff-calendar.component").then((m) => m.StaffCalendarComponent),
        title: "Appointments",
        canActivate: [portalPermissionGuard],
        data: { teamCalendar: true },
      },
      {
        path: "my-calendar",
        loadComponent: () => import("./pages/staff/staff-calendar.component").then((m) => m.StaffCalendarComponent),
        title: "My calendar",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "quotes",
        loadComponent: () => import("./pages/quotes/quotes.component").then((m) => m.QuotesComponent),
        title: "Quotes",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "messages",
        loadComponent: () => import("./pages/messages/messages.component").then((m) => m.MessagesComponent),
        title: "Messages",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "services",
        loadComponent: () => import("./pages/services/services.component").then((m) => m.ServicesComponent),
        title: "Services",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "staff",
        loadComponent: () => import("./pages/staff/staff.component").then((m) => m.StaffComponent),
        title: "Staff",
        canActivate: [portalPermissionGuard],
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
        canActivate: [portalPermissionGuard],
      },
      {
        path: "availability",
        loadComponent: () => import("./pages/availability/availability.component").then((m) => m.AvailabilityComponent),
        title: "Hours",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "profile",
        loadComponent: () => import("./pages/profile/profile.component").then((m) => m.ProfileComponent),
        title: "Profile",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "public-page",
        loadComponent: () => import("./pages/public-page/public-page.component").then((m) => m.PublicPageSettingsComponent),
        title: "Public page",
        canActivate: [portalPermissionGuard],
      },
      { path: "register", loadComponent: () => import("./pages/register/register.component").then((m) => m.RegisterComponent), title: "Register" },
      {
        path: "finance",
        loadComponent: () => import("./pages/payments/payments.component").then((m) => m.PaymentsComponent),
        title: "Finance",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "settings",
        loadComponent: () => import("./pages/settings/settings.component").then((m) => m.SettingsComponent),
        title: "Settings",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "settings/notifications",
        loadComponent: () => import("./pages/settings/settings.component").then((m) => m.SettingsComponent),
        title: "Notification settings",
        canActivate: [portalPermissionGuard],
        data: { settingsTab: "notifications" },
      },
      {
        path: "plan",
        loadComponent: () => import("./pages/plan/plan.component").then((m) => m.PlanComponent),
        title: "Plan",
        canActivate: [portalPermissionGuard],
      },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
