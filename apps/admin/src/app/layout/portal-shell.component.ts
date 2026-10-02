import { Component, inject } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { AuthService } from "@auth0/auth0-angular";
import { AsyncPipe } from "@angular/common";
import { ADMIN_NAV } from "../core/admin-nav";
import { ADENI_ADMIN_CONFIG, isAdminPortalDevMode, isAuth0Configured } from "../core/adeni-config";

@Component({
  selector: "app-admin-shell",
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, AsyncPipe],
  templateUrl: "./portal-shell.component.html",
  styleUrl: "./portal-shell.component.scss",
})
export class AdminShellComponent {
  readonly nav = ADMIN_NAV;
  readonly config = inject(ADENI_ADMIN_CONFIG);
  readonly auth = inject(AuthService, { optional: true });

  readonly devMode = isAdminPortalDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);

  logout(): void {
    this.auth?.logout({
      logoutParams: { returnTo: window.location.origin },
    });
  }
}
