import { Component } from "@angular/core";
import { RouterLink } from "@angular/router";

import { injectAdeniTranslate } from "@adeni/ui";
@Component({
  selector: "app-forbidden",
  standalone: true,
  imports: [RouterLink],
  template: `
    <section class="card">
      <h2>{{ tr('common.businessAccessRequired') }}</h2>
      <p>{{ tr('common.yourAccountDoesNotHave') }}</p>
      <a routerLink="/dashboard">{{ tr('common.back') }}</a>
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
export class ForbiddenComponent {
  protected readonly tr = injectAdeniTranslate();}
