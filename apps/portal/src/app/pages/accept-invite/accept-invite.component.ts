import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { AdeniFeedbackService } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import {
  ADENI_PORTAL_CONFIG,
  isAuth0Configured,
  isBusinessPortalDevMode,
} from "../../core/adeni-config";

@Component({
  selector: "app-accept-invite",
  imports: [RouterLink, FormsModule],
  templateUrl: "./accept-invite.component.html",
  styleUrl: "./accept-invite.component.scss",
})
export class AcceptInviteComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly status = signal<"ready" | "working" | "done" | "error">("ready");
  readonly message = signal(
    "Sign in (or use the staff Dev sub), then accept to join this business without creating a new one.",
  );
  readonly devMode = isBusinessPortalDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);

  /** Dev-only: which Auth0 sub accepts the invite (defaults to seeded staff sub). */
  devAuth0Sub = "";
  private token = "";

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get("token")?.trim() ?? "";
    this.devAuth0Sub =
      this.route.snapshot.queryParamMap.get("devSub")?.trim()
      || this.config.devStaffAuth0Sub
      || "auth0|local-staff";

    if (!this.token) {
      this.status.set("error");
      this.message.set("This invite link is missing a token. Ask the owner to resend the invite.");
    }
  }

  tokenReady(): boolean {
    return this.token.length > 0;
  }

  async accept(): Promise<void> {
    if (!this.token || this.status() === "working") {
      return;
    }

    this.status.set("working");
    try {
      const result = await this.api.withInviteClient(
        async (client) => client.acceptStaffInvite({ token: this.token }),
        this.devMode ? this.devAuth0Sub : undefined,
      );
      this.status.set("done");
      this.message.set(`You're in as ${result.permissionRole}. Opening the portal…`);
      this.feedback.success("Invite accepted");
      await this.router.navigateByUrl("/dashboard");
    } catch {
      this.status.set("error");
      this.message.set(
        "Could not accept this invite. It may be expired, revoked, or meant for a different email.",
      );
      this.feedback.error("Invite not accepted");
    }
  }
}
