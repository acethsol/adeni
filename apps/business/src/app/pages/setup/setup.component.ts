import { Component, inject } from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import { ADENI_PORTAL_CONFIG, isBusinessPortalDevMode } from "../../core/adeni-config";
import { isPortalDevSignedOut, signInPortalDev } from "../../core/business-dev-session";

import { injectAdeniTranslate } from "@adeni/ui";
@Component({
  selector: "app-setup",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./setup.component.html",
  styleUrl: "./setup.component.scss",
})
export class SetupComponent {
  protected readonly tr = injectAdeniTranslate();
  private readonly router = inject(Router);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  readonly signedOut = isBusinessPortalDevMode(this.config) && isPortalDevSignedOut();

  signIn(): void {
    signInPortalDev();
    void this.router.navigateByUrl("/dashboard");
  }
}
