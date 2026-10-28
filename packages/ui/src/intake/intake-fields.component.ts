import { Component, computed, input, output } from "@angular/core";
import type { IntakeAnswer, IntakeDefinition, IntakeField } from "@adeni/shared";
import { groupIntakeFields, intakeFieldGroupTrack } from "@adeni/shared";
import { injectAdeniTranslate } from "../locale/adeni-i18n";

/** Presentation only: the server owns validation, access and completion policy. No persistent storage. */
@Component({
  selector: "adeni-intake-fields",
  standalone: true,
  imports: [],
  styleUrl: "./intake-fields.component.scss",
  template: `
    <div class="adeni-intake-fields">
      @for (group of groups(); track trackGroup(group)) {
        <div class="intake-group" [class.intake-group--section]="group.kind === 'section'">
          @if (group.kind === 'section') {
            <header class="intake-section__head">
              <h3 class="intake-section__title">{{ group.section.title }}</h3>
              @if (group.section.description) {
                <p class="intake-section__description">{{ group.section.description }}</p>
              }
            </header>
          }
          @for (field of group.fields; track field.id) {
            <div class="field" [class.field--consent]="field.type === 6">
              @if (field.type !== 6) {
                <label [attr.for]="controlId(field)">
                  {{ field.label }}
                  @if (field.required) {
                    <span class="field__required" [attr.aria-label]="tr('intake.required')">*</span>
                  }
                </label>
                @if (field.helpText) {
                  <p class="field__help">{{ field.helpText }}</p>
                }
              }
              @if (readOnly() && !preview()) {
                <p class="field__value">{{ display(field) }}</p>
              } @else {
                @switch (field.type) {
                  @case (0) {
                    <input
                      [id]="controlId(field)"
                      type="text"
                      maxlength="250"
                      [required]="field.required"
                      [disabled]="readOnly() || preview()"
                      [value]="value(field)?.text ?? ''"
                      (input)="set(field.id, { text: $any($event.target).value })"
                    />
                  }
                  @case (1) {
                    <textarea
                      [id]="controlId(field)"
                      maxlength="4000"
                      [required]="field.required"
                      [disabled]="readOnly() || preview()"
                      [value]="value(field)?.text ?? ''"
                      (input)="set(field.id, { text: $any($event.target).value })"
                    ></textarea>
                  }
                  @case (2) {
                    <select
                      [id]="controlId(field)"
                      [required]="field.required"
                      [disabled]="readOnly() || preview()"
                      [value]="value(field)?.choices?.[0] ?? ''"
                      (change)="set(field.id, { choices: $any($event.target).value ? [$any($event.target).value] : [] })"
                    >
                      <option value="">{{ tr('intake.choose') }}</option>
                      @for (choice of field.choices; track choice.id) {
                        <option [value]="choice.id">{{ choice.label }}</option>
                      }
                    </select>
                  }
                  @case (3) {
                    <div class="field__choice-group" role="group" [attr.aria-label]="field.label">
                      @for (choice of field.choices; track choice.id) {
                        <label class="field__choice">
                          <input
                            type="checkbox"
                            [disabled]="readOnly() || preview()"
                            [checked]="value(field)?.choices?.includes(choice.id) ?? false"
                            (change)="toggle(field, choice.id, $any($event.target).checked)"
                          />
                          {{ choice.label }}
                        </label>
                      }
                    </div>
                  }
                  @case (4) {
                    <select
                      [id]="controlId(field)"
                      [required]="field.required"
                      [disabled]="readOnly() || preview()"
                      [value]="value(field)?.boolean == null ? '' : value(field)?.boolean ? 'true' : 'false'"
                      (change)="boolean(field.id, $any($event.target).value)"
                    >
                      <option value="">{{ tr('intake.choose') }}</option>
                      <option value="true">{{ tr('intake.yes') }}</option>
                      <option value="false">{{ tr('intake.no') }}</option>
                    </select>
                  }
                  @case (5) {
                    <input
                      [id]="controlId(field)"
                      type="date"
                      [required]="field.required"
                      [disabled]="readOnly() || preview()"
                      [value]="value(field)?.date ?? ''"
                      (input)="date(field.id, $any($event.target).value)"
                    />
                  }
                  @case (6) {
                    <label class="field__consent-label">
                      <input
                        type="checkbox"
                        [required]="field.required"
                        [disabled]="readOnly() || preview()"
                        [checked]="value(field)?.boolean ?? false"
                        (change)="set(field.id, { boolean: $any($event.target).checked })"
                      />
                      <span>
                        {{ field.label }}
                        @if (field.required) {
                          <span class="field__required" [attr.aria-label]="tr('intake.required')">*</span>
                        }
                      </span>
                    </label>
                    @if (field.helpText) {
                      <p class="field__help">{{ field.helpText }}</p>
                    }
                  }
                }
              }
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class IntakeFieldsComponent {
  readonly definition = input.required<IntakeDefinition>();
  readonly answers = input<readonly IntakeAnswer[]>([]);
  readonly readOnly = input(false);
  /** Disabled controls with empty values — guest-facing layout without answers (e.g. published schema preview). */
  readonly preview = input(false);
  readonly answersChange = output<IntakeAnswer[]>();
  readonly tr = injectAdeniTranslate();

  readonly groups = computed(() => groupIntakeFields(this.definition()));

  trackGroup = intakeFieldGroupTrack;

  controlId(field: IntakeField): string {
    return `adeni-intake-${field.id}`;
  }

  value(field: IntakeField): IntakeAnswer | undefined {
    return this.answers().find(x => x.fieldId === field.id);
  }

  set(id: string, value: Omit<IntakeAnswer, "fieldId">): void {
    this.answersChange.emit([...this.answers().filter(x => x.fieldId !== id), { fieldId: id, ...value }]);
  }

  boolean(id: string, value: string): void {
    if (!value) this.clear(id);
    else this.set(id, { boolean: value === "true" });
  }

  date(id: string, value: string): void {
    if (!value) this.clear(id);
    else this.set(id, { date: value });
  }

  private clear(id: string): void {
    this.answersChange.emit(this.answers().filter(x => x.fieldId !== id));
  }

  toggle(field: IntakeField, id: string, checked: boolean): void {
    const current = this.value(field)?.choices ?? [];
    this.set(field.id, { choices: checked ? [...new Set([...current, id])] : current.filter(x => x !== id) });
  }

  display(field: IntakeField): string {
    const value = this.value(field);
    if (!value) return this.tr("intake.noAnswer");
    if (value.text != null) return value.text;
    if (value.boolean != null) return this.tr(value.boolean ? "intake.yes" : "intake.no");
    if (value.date) return value.date;
    return field.choices
      .filter(x => value.choices?.includes(x.id))
      .map(x => x.label)
      .join(", ");
  }
}
