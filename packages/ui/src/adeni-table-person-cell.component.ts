import { Component, input } from "@angular/core";

@Component({
  selector: "adeni-table-person-cell",
  standalone: true,
  template: `
    <div class="adeni-cell-person">
      <div class="adeni-cell-person-avatar" [attr.data-tone]="avatarTone()">
        @if (avatarUrl()?.trim()) {
          <img [src]="avatarUrl()" alt="" loading="lazy" decoding="async" />
        } @else {
          <span class="adeni-cell-person-initials">{{ initials() }}</span>
        }
      </div>
      <div class="adeni-cell-person-copy">
        <span class="adeni-cell-person-name">{{ displayName() }}</span>
        @if (subtitle()?.trim()) {
          <span class="adeni-cell-person-sub">{{ subtitle() }}</span>
        }
      </div>
    </div>
  `,
  styleUrl: "./styles/_data-grid-person-cell.scss",
})
export class AdeniTablePersonCellComponent {
  readonly displayName = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly avatarUrl = input<string | null>(null);
  readonly initials = input("?");
  readonly avatarTone = input("neutral");
}
