import { Routes } from "@angular/router";
import { PublicShellComponent } from "./layout/public-shell.component";
import { HomeComponent } from "./pages/home/home.component";
import { DiscoverComponent } from "./pages/discover/discover.component";
import { BusinessProfileComponent } from "./pages/business/business.component";
import { MyBookingsComponent } from "./pages/my-bookings/my-bookings.component";
import { customerAuthGuard } from "./core/guards/customer-auth.guard";

export const routes: Routes = [
  {
    path: "",
    component: PublicShellComponent,
    children: [
      { path: "", pathMatch: "full", component: HomeComponent },
      { path: "discover", component: DiscoverComponent },
      { path: "businesses/:slug", component: BusinessProfileComponent },
      {
        path: "my-bookings",
        canActivate: [customerAuthGuard],
        component: MyBookingsComponent,
      },
    ],
  },
  { path: "**", redirectTo: "" },
];
