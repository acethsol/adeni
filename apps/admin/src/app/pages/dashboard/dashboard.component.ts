import { Component } from "@angular/core";
import { RouterLink } from "@angular/router";
import { PortalPageComponent } from "@adeni/ui";

@Component({
  selector: "app-admin-dashboard",
  standalone: true,
  imports: [PortalPageComponent, RouterLink],
  template: `
    <app-portal-page
      title="Admin dashboard"
      description="Verification queue, markets, and business subscription tools."
    >
      <ul class="links">
        <li><a routerLink="/pending">Review pending businesses</a></li>
        <li><a routerLink="/markets">Manage markets</a></li>
        <li><a routerLink="/businesses">Business subscription overrides</a></li>
      </ul>
    </app-portal-page>
  `,
  styles: `
    .links {
      margin: 0;
      padding-left: 1.25rem;
      line-height: 1.8;
    }
  `,
})
export class AdminDashboardComponent {}
