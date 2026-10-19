import { Directive, inject, input, TemplateRef } from "@angular/core";

/** Custom cell renderer; bind column id: `adeniDataTableCell="name"`. Context: `{ $implicit: row }`. */
@Directive({
  selector: "[adeniDataTableCell]",
  standalone: true,
})
export class AdeniDataTableCellDirective {
  readonly columnId = input.required<string>({ alias: "adeniDataTableCell" });
  readonly template = inject(TemplateRef<{ $implicit: unknown }>);
}
