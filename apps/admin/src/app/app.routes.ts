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
  { path: "setup", component: SetupComponent },
  { path: "forbidden", component: ForbiddenComponent },
  {
    path: "",
    canActivate: [adminAccessGuard, adminAuthGuard],
    component: AdminShellComponent,
    children: [
      { path: "", pathMatch: "full", redirectTo: "dashboard" },
      { path: "dashboard", component: AdminDashboardComponent },
      { path: "pending", component: AdminPendingComponent },
      { path: "markets", component: AdminMarketsComponent },
      { path: "businesses", component: AdminBusinessesComponent },
      { path: "customers", component: AdminCustomersComponent },
    ],
  },
  { path: "**", redirectTo: "dashboard" },
];
