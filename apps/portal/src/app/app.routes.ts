import { Routes } from "@angular/router";
import { PortalShellComponent } from "./layout/portal-shell.component";
import { DashboardComponent } from "./pages/dashboard/dashboard.component";
import { BookingsComponent } from "./pages/bookings/bookings.component";
import { ServicesComponent } from "./pages/services/services.component";
import { StaffComponent } from "./pages/staff/staff.component";
import { StaffCalendarComponent } from "./pages/staff/staff-calendar.component";
import { LocationsComponent } from "./pages/locations/locations.component";
import { AvailabilityComponent } from "./pages/availability/availability.component";
import { ProfileComponent } from "./pages/profile/profile.component";
import { PublicPageSettingsComponent } from "./pages/public-page/public-page.component";
import { RegisterComponent } from "./pages/register/register.component";
import { PaymentsComponent } from "./pages/payments/payments.component";
import { PlanComponent } from "./pages/plan/plan.component";
import { MessagesComponent } from "./pages/messages/messages.component";
import { QuotesComponent } from "./pages/quotes/quotes.component";
import { SetupComponent } from "./pages/setup/setup.component";
import { ForbiddenComponent } from "./pages/forbidden/forbidden.component";
import { portalAccessGuard } from "./core/guards/portal-access.guard";
import { businessAuthGuard } from "./core/guards/business-auth.guard";
import { portalPermissionGuard } from "./core/guards/portal-permission.guard";

export const routes: Routes = [
  { path: "setup", component: SetupComponent, title: "Setup" },
  { path: "forbidden", component: ForbiddenComponent, title: "Access denied" },
  {
    path: "",
    canActivate: [portalAccessGuard, businessAuthGuard],
    component: PortalShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      {
        path: "dashboard",
        component: DashboardComponent,
        title: "Dashboard",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "bookings",
        component: BookingsComponent,
        title: "Bookings",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "quotes",
        component: QuotesComponent,
        title: "Quotes",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "messages",
        component: MessagesComponent,
        title: "Messages",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "services",
        component: ServicesComponent,
        title: "Services",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "staff",
        component: StaffComponent,
        title: "Staff",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "staff/:id/calendar",
        component: StaffCalendarComponent,
        title: "Staff calendar",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "locations",
        component: LocationsComponent,
        title: "Locations",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "availability",
        component: AvailabilityComponent,
        title: "Availability",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "profile",
        component: ProfileComponent,
        title: "Profile",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "public-page",
        component: PublicPageSettingsComponent,
        title: "Public page",
        canActivate: [portalPermissionGuard],
      },
      { path: "register", component: RegisterComponent, title: "Register" },
      {
        path: "payments",
        component: PaymentsComponent,
        title: "Payments",
        canActivate: [portalPermissionGuard],
      },
      {
        path: "plan",
        component: PlanComponent,
        title: "Plan",
        canActivate: [portalPermissionGuard],
      },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
