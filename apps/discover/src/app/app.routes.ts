import { Routes } from "@angular/router";
import { PublicShellComponent } from "./layout/public-shell.component";
import { HomeComponent } from "./pages/home/home.component";
import { DiscoverComponent } from "./pages/discover/discover.component";
import { BusinessProfileComponent } from "./pages/business/business.component";

export const routes: Routes = [
  {
    path: "",
    component: PublicShellComponent,
    children: [
      { path: "", pathMatch: "full", component: HomeComponent },
      { path: "discover", component: DiscoverComponent },
      { path: "businesses/:slug", component: BusinessProfileComponent },
    ],
  },
  { path: "**", redirectTo: "" },
];
