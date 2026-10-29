import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { AdeniFeedbackService, injectAdeniTranslate, injectAdeniApiError } from "@adeni/ui";
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
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly status = signal<"ready" | "working" | "done" | "error">("ready");
  readonly message = signal(
    this.tr(
      isBusinessPortalDevMode(this.config)
        ? "businessApp.acceptInvite.signInThenAcceptDev"
        : "businessApp.acceptInvite.signInThenAccept",
    ),
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
      this.message.set(this.tr("businessApp.acceptInvite.thisInviteLinkMissingToken"));
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
      this.message.set(this.tr("businessApp.acceptInvite.youReAsOpeningPortal", { permissionRole: result.permissionRole }));
      this.feedback.success(this.tr("businessApp.acceptInvite.inviteAccepted"));
      await this.router.navigateByUrl("/dashboard");
    } catch (caught) {
      this.status.set("error");
      this.message.set(
        this.tr("businessApp.acceptInvite.couldNotAcceptInviteMay"),
      );
      this.feedback.error(this.apiErr(caught, "businessApp.acceptInvite.inviteNotAccepted"));
    }
  }
}
