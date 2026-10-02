import { Component, inject } from "@angular/core";
import { ActivatedRoute } from "@angular/router";

@Component({
  selector: "app-placeholder-page",
  standalone: true,
  template: `
    <section class="card">
      <h2>{{ title }}</h2>
      <p>
        Screen parity with Next.js <code>/business/{{ slug }}</code> is planned in the Angular
        strangler migration.
      </p>
    </section>
  `,
  styles: `
    .card {
      padding: 1.25rem 1.5rem;
      border-radius: 1rem;
      background: var(--surface);
      border: 1px solid var(--border);
    }
    h2 {
      margin: 0 0 0.5rem;
    }
    p {
      margin: 0;
      color: var(--text-muted);
    }
  `,
})
export class PlaceholderPageComponent {
  private readonly route = inject(ActivatedRoute);
  readonly title = this.route.snapshot.data["title"] as string;
  readonly slug = this.route.snapshot.data["slug"] as string;
}
