import { Directive, inject, TemplateRef } from "@angular/core";

/** Marks the actions column template; context: `{ $implicit: row }`. */
@Directive({
  selector: "[adeniDataTableActions]",
  standalone: true,
})
export class AdeniDataTableActionsDirective {
  readonly template = inject(TemplateRef<{ $implicit: unknown }>);
}
