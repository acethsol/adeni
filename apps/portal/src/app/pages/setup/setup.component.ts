import { Component, inject } from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import { ADENI_PORTAL_CONFIG, isBusinessPortalDevMode } from "../../core/adeni-config";
import { isPortalDevSignedOut, signInPortalDev } from "../../core/portal-dev-session";

@Component({
  selector: "app-setup",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./setup.component.html",
  styleUrl: "./setup.component.scss",
})
export class SetupComponent {
  private readonly router = inject(Router);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  readonly signedOut = isBusinessPortalDevMode(this.config) && isPortalDevSignedOut();

  signIn(): void {
    signInPortalDev();
    void this.router.navigateByUrl("/dashboard");
  }
}
