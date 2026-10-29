import { AfterViewInit, Directive, ElementRef, inject } from "@angular/core";

/**
 * Native hover tooltip for icon-only header controls (uses existing aria-label).
 */
@Directive({
  selector: "button.header-btn, button.kiosk-toolbar-btn--icon, a.bell",
})
export class BusinessIconTooltipDirective implements AfterViewInit {
  private readonly element = inject(ElementRef<HTMLElement>);

  ngAfterViewInit(): void {
    const node = this.element.nativeElement;
    if (node.getAttribute("title")) {
      return;
    }
    const visibleText = (node.textContent ?? "").replace(/\s+/g, "").length > 0;
    if (visibleText) {
      return;
    }
    const label = node.getAttribute("aria-label");
    if (label) {
      node.setAttribute("title", label);
    }
  }
}
