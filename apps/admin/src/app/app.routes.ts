import { Routes } from "@angular/router";
import { AdminShellComponent } from "./layout/portal-shell.component";
import { AdminDashboardComponent } from "./pages/dashboard/dashboard.component";
import { AdminPendingComponent } from "./pages/pending/pending.component";
import { AdminMarketsComponent } from "./pages/markets/markets.component";
import { AdminBusinessesComponent } from "./pages/businesses/businesses.component";
import { AdminCustomersComponent } from "./pages/customers/customers.component";
import { SetupComponent } from "./pages/setup/setup.component";
import { ForbiddenComponent } from "./pages/forbidden/forbidden.component";
import { adminAccessGuard } from "./core/guards/admin-access.guard";
import { adminAuthGuard } from "./core/guards/admin-auth.guard";

export const routes: Routes = [
  { path: "setup", component: SetupComponent, title: "Setup" },
  { path: "forbidden", component: ForbiddenComponent, title: "Access denied" },
  {
    path: "",
    canActivate: [adminAccessGuard, adminAuthGuard],
    component: AdminShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "dashboard", component: AdminDashboardComponent, title: "Dashboard" },
      { path: "pending", component: AdminPendingComponent, title: "Pending verifications" },
      { path: "markets", component: AdminMarketsComponent, title: "Markets" },
      { path: "businesses", component: AdminBusinessesComponent, title: "Businesses" },
      { path: "customers", component: AdminCustomersComponent, title: "Customers" },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
