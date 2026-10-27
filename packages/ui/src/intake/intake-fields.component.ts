import { Component, input, output } from "@angular/core";
import type { IntakeAnswer, IntakeDefinition, IntakeField } from "@adeni/shared";
import { injectAdeniTranslate } from "../locale/adeni-i18n";

/** Presentation only: the server owns validation, access and completion policy. No persistent storage. */
@Component({
  selector: "adeni-intake-fields", standalone: true, imports: [],
  styles: `fieldset { border: 0; padding: 0; margin: 1rem 0; } label, small { display: block; margin-block: .25rem; } input:not([type=checkbox]), select, textarea { max-width: 100%; padding: .5rem; }`,
  template: `
    @for (field of definition().fields; track field.id) {
      <fieldset [disabled]="readOnly()"><legend>{{ field.label }} @if(field.required) { <span aria-label="required">*</span> }</legend>
      @if(field.helpText) { <small>{{field.helpText}}</small> }
      @if (readOnly()) { <p>{{display(field)}}</p> }
      @else {
        @switch(field.type) {
          @case(0) { <input type="text" [attr.aria-label]="field.label" maxlength="250" [required]="field.required" [value]="value(field)?.text ?? ''"  (input)="set(field.id, {text:$any($event.target).value})" /> }
          @case(1) { <textarea [attr.aria-label]="field.label" maxlength="4000" [required]="field.required" [value]="value(field)?.text ?? ''"  (input)="set(field.id, {text:$any($event.target).value})"></textarea> }
          @case(2) { <select [attr.aria-label]="field.label" [required]="field.required" [value]="value(field)?.choices?.[0] ?? ''"  (change)="set(field.id, {choices:$any($event.target).value ? [$any($event.target).value] : []})"><option value="">{{tr('intake.choose')}}</option>@for(choice of field.choices; track choice.id) { <option [value]="choice.id">{{choice.label}}</option> }</select> }
          @case(3) { @for(choice of field.choices; track choice.id) { <label><input type="checkbox" [checked]="value(field)?.choices?.includes(choice.id) ?? false" (change)="toggle(field, choice.id, $any($event.target).checked)" /> {{choice.label}}</label> } }
          @case(4) { <select [attr.aria-label]="field.label" [required]="field.required" [value]="value(field)?.boolean == null ? '' : value(field)?.boolean ? 'true' : 'false'"  (change)="boolean(field.id,$any($event.target).value)"><option value="">{{tr('intake.choose')}}</option><option value="true">{{tr('intake.yes')}}</option><option value="false">{{tr('intake.no')}}</option></select> }
          @case(5) { <input type="date" [attr.aria-label]="field.label" [required]="field.required" [value]="value(field)?.date ?? ''"  (input)="date(field.id,$any($event.target).value)" /> }
          @case(6) { <label><input type="checkbox" [required]="field.required" [checked]="value(field)?.boolean ?? false" (change)="set(field.id, {boolean:$any($event.target).checked})" />{{field.label}}</label> }
        }
      }</fieldset>
    }
  `,
})
export class IntakeFieldsComponent {
  readonly definition = input.required<IntakeDefinition>();
  readonly answers = input<readonly IntakeAnswer[]>([]);
  readonly readOnly = input(false);
  readonly answersChange = output<IntakeAnswer[]>();
  readonly tr = injectAdeniTranslate();
  value(field: IntakeField): IntakeAnswer | undefined { return this.answers().find(x => x.fieldId === field.id); }
  set(id: string, value: Omit<IntakeAnswer, "fieldId">): void { this.answersChange.emit([...this.answers().filter(x => x.fieldId !== id), { fieldId: id, ...value }]); }
  boolean(id: string, value: string): void { if (!value) this.clear(id); else this.set(id, {boolean:value === "true"}); }
  date(id: string, value: string): void { if (!value) this.clear(id); else this.set(id, {date:value}); }
  private clear(id: string): void { this.answersChange.emit(this.answers().filter(x => x.fieldId !== id)); }
  toggle(field: IntakeField, id: string, checked: boolean): void {
    const current = this.value(field)?.choices ?? [];
    this.set(field.id, {choices: checked ? [...new Set([...current, id])] : current.filter(x => x !== id)});
  }
  display(field: IntakeField): string {
    const value = this.value(field);
    if (!value) return this.tr('intake.noAnswer');
    if (value.text != null) return value.text;
    if (value.boolean != null) return this.tr(value.boolean ? 'intake.yes' : 'intake.no');
    if (value.date) return value.date;
    return field.choices.filter(x => value.choices?.includes(x.id)).map(x => x.label).join(', ');
  }
}
