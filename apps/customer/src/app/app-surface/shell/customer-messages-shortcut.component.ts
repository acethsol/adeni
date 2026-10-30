import { Component, inject } from "@angular/core";
import { RouterLink } from "@angular/router";
import { injectAdeniTranslate } from "@adeni/ui";
import { CustomerMessagingBadgeService } from "../services/customer-messaging-badge.service";

/** Top-bar messages entry — same Carbon chat glyph + badge treatment as the business portal. */
@Component({
  selector: "app-customer-messages-shortcut",
  standalone: true,
  imports: [RouterLink],
  template: `
    <a
      class="bell"
      routerLink="/app/messages"
      [attr.aria-label]="badge.unread() ? badge.unread() + ' unread messages' : tr('common.messages')"
    >
      <svg viewBox="0 0 32 32" width="20" height="20" aria-hidden="true">
        <path
          fill="currentColor"
          d="M17.74,30,16,29l4-7h6a2,2,0,0,0,2-2V8a2,2,0,0,0-2-2H4A2,2,0,0,0,2,8V20a2,2,0,0,0,2,2h8.4ZM4,8H28V20H19.16l-2.72,4.76L13.6,20H4Z"
        />
      </svg>
      @if (badge.unread(); as count) {
        @if (count > 0) {
          <span class="badge">{{ count > 9 ? "9+" : count }}</span>
        }
      }
    </a>
  `,
  styles: `
    .bell {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 3rem;
      height: 3rem;
      color: var(--text-muted);
      text-decoration: none;
    }

    .bell:hover {
      background: rgb(127 86 255 / 8%);
      color: var(--text);
    }

    .badge {
      position: absolute;
      top: 0.15rem;
      right: 0.15rem;
      min-width: 1rem;
      height: 1rem;
      padding: 0 0.25rem;
      border-radius: 999px;
      background: var(--interactive, #7f56ff);
      color: #fff;
      font-size: 0.625rem;
      font-weight: 700;
      line-height: 1rem;
      text-align: center;
    }
  `,
})
export class CustomerMessagesShortcutComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly badge = inject(CustomerMessagingBadgeService);
}
