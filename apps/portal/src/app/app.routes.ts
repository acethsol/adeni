import { Routes } from "@angular/router";
import { PortalShellComponent } from "./layout/portal-shell.component";
import { DashboardComponent } from "./pages/dashboard/dashboard.component";
import { BookingsComponent } from "./pages/bookings/bookings.component";
import { ServicesComponent } from "./pages/services/services.component";
import { StaffComponent } from "./pages/staff/staff.component";
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

export const routes: Routes = [
  { path: "setup", component: SetupComponent, title: "Setup" },
  { path: "forbidden", component: ForbiddenComponent, title: "Access denied" },
  {
    path: "",
    canActivate: [portalAccessGuard, businessAuthGuard],
    component: PortalShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "dashboard", component: DashboardComponent, title: "Dashboard" },
      { path: "bookings", component: BookingsComponent, title: "Bookings" },
      { path: "quotes", component: QuotesComponent, title: "Quotes" },
      { path: "messages", component: MessagesComponent, title: "Messages" },
      { path: "services", component: ServicesComponent, title: "Services" },
      { path: "staff", component: StaffComponent, title: "Staff" },
      { path: "locations", component: LocationsComponent, title: "Locations" },
      { path: "availability", component: AvailabilityComponent, title: "Availability" },
      { path: "profile", component: ProfileComponent, title: "Profile" },
      { path: "public-page", component: PublicPageSettingsComponent, title: "Public page" },
      { path: "register", component: RegisterComponent, title: "Register" },
      { path: "payments", component: PaymentsComponent, title: "Payments" },
      { path: "plan", component: PlanComponent, title: "Plan" },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
