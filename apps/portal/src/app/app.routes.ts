import { Routes } from "@angular/router";
import { PortalShellComponent } from "./layout/portal-shell.component";
import { DashboardComponent } from "./pages/dashboard/dashboard.component";
import { BookingsComponent } from "./pages/bookings/bookings.component";
import { ServicesComponent } from "./pages/services/services.component";
import { LocationsComponent } from "./pages/locations/locations.component";
import { AvailabilityComponent } from "./pages/availability/availability.component";
import { ProfileComponent } from "./pages/profile/profile.component";
import { RegisterComponent } from "./pages/register/register.component";
import { PaymentsComponent } from "./pages/payments/payments.component";
import { PlanComponent } from "./pages/plan/plan.component";
import { MessagesComponent } from "./pages/messages/messages.component";
import { SetupComponent } from "./pages/setup/setup.component";
import { ForbiddenComponent } from "./pages/forbidden/forbidden.component";
import { portalAccessGuard } from "./core/guards/portal-access.guard";
import { businessAuthGuard } from "./core/guards/business-auth.guard";

export const routes: Routes = [
  { path: "setup", component: SetupComponent },
  { path: "forbidden", component: ForbiddenComponent },
  {
    path: "",
    canActivate: [portalAccessGuard, businessAuthGuard],
    component: PortalShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "dashboard", component: DashboardComponent },
      { path: "bookings", component: BookingsComponent },
      { path: "messages", component: MessagesComponent },
      { path: "services", component: ServicesComponent },
      { path: "locations", component: LocationsComponent },
      { path: "availability", component: AvailabilityComponent },
      { path: "profile", component: ProfileComponent },
      { path: "register", component: RegisterComponent },
      { path: "payments", component: PaymentsComponent },
      { path: "plan", component: PlanComponent },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
