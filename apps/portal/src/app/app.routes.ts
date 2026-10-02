import { Routes } from "@angular/router";
import { PortalShellComponent } from "./layout/portal-shell.component";
import { DashboardComponent } from "./pages/dashboard/dashboard.component";
import { PlaceholderPageComponent } from "./pages/placeholder/placeholder-page.component";
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
      {
        path: "bookings",
        component: PlaceholderPageComponent,
        data: { title: "Bookings", slug: "bookings" },
      },
      {
        path: "services",
        component: PlaceholderPageComponent,
        data: { title: "Services", slug: "services" },
      },
      {
        path: "locations",
        component: PlaceholderPageComponent,
        data: { title: "Locations", slug: "locations" },
      },
      {
        path: "availability",
        component: PlaceholderPageComponent,
        data: { title: "Availability", slug: "availability" },
      },
      {
        path: "profile",
        component: PlaceholderPageComponent,
        data: { title: "Profile", slug: "profile" },
      },
      {
        path: "payments",
        component: PlaceholderPageComponent,
        data: { title: "Payments", slug: "payments" },
      },
      {
        path: "plan",
        component: PlaceholderPageComponent,
        data: { title: "Plan", slug: "plan" },
      },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
