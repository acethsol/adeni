import { Component, inject, input, output } from "@angular/core";
import { RouterLink } from "@angular/router";
import { AdeniStaffButtonComponent, injectAdeniTranslate } from "@adeni/ui";

@Component({
  selector: "app-presence-required-panel",
  standalone: true,
  imports: [AdeniStaffButtonComponent, RouterLink],
  template: `
    <section class="presence-required">
      <h3>{{ tr("businessApp.attendance.presenceRequiredTitle") }}</h3>
      <p>{{ tr("businessApp.attendance.presenceRequiredBody") }}</p>
      <adeni-staff-btn icon="location" (pressed)="verify.emit()">
        {{ tr("businessApp.attendance.presenceVerifyAction") }}
      </adeni-staff-btn>
      @if (showAttendanceLink()) {
        <p class="hint">
          <a routerLink="/attendance">{{ tr("businessApp.attendance.myTitle") }}</a>
        </p>
      }
    </section>
  `,
  styles: [
    `
      .presence-required {
        padding: 1.25rem;
        border: 1px solid var(--cds-border-subtle-01, #e0e0e0);
        border-radius: 8px;
        margin-bottom: 1rem;
      }
      .hint {
        margin-top: 0.75rem;
        font-size: 0.875rem;
      }
    `,
  ],
})
export class PresenceRequiredPanelComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly showAttendanceLink = input(true);
  readonly verify = output<void>();
}
