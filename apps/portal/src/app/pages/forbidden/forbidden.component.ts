import { Component } from "@angular/core";
import { RouterLink } from "@angular/router";

@Component({
  selector: "app-forbidden",
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="card">
      <h2>Business access required</h2>
      <p>Your account does not have the business role. Use a business login or contact support.</p>
      <a routerLink="/dashboard">Back</a>
    </section>
  `,
  styles: `
    .card {
      padding: 1.5rem;
      border-radius: 1rem;
      background: var(--surface);
      border: 1px solid var(--border);
    }
  `,
})
export class ForbiddenComponent {}
