import { Component, inject } from "@angular/core";
import { Router } from "@angular/router";
import { isAdminDevSignedOut, signInAdminDev } from "../../core/admin-dev-session";
import { ADENI_ADMIN_CONFIG, isAdminPortalDevMode } from "../../core/adeni-config";

@Component({
  selector: "app-setup",
  standalone: true,
  templateUrl: "./setup.component.html",
  styleUrl: "./setup.component.scss",
})
export class SetupComponent {
  private readonly router = inject(Router);
  private readonly config = inject(ADENI_ADMIN_CONFIG);
  readonly signedOut = isAdminPortalDevMode(this.config) && isAdminDevSignedOut();

  signIn(): void {
    signInAdminDev();
    void this.router.navigateByUrl("/dashboard");
  }
}
