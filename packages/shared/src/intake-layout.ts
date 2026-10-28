import type { IntakeDefinition, IntakeField, IntakeSection } from "./intake";

export type IntakeFieldGroup =
  | { kind: "default"; fields: IntakeField[] }
  | { kind: "section"; section: IntakeSection; fields: IntakeField[] };

/** Preserves field order within each group; unsectioned fields render before section blocks. */
export function groupIntakeFields(definition: IntakeDefinition): IntakeFieldGroup[] {
  const sections = [...(definition.sections ?? [])].sort((a, b) => a.sortOrder - b.sortOrder);
  const fields = definition.fields;
  const groups: IntakeFieldGroup[] = [];
  const unsectioned = fields.filter((f) => !f.sectionId);
  if (unsectioned.length > 0) {
    groups.push({ kind: "default", fields: unsectioned });
  }
  for (const section of sections) {
    const sectionFields = fields.filter((f) => f.sectionId === section.id);
    if (sectionFields.length > 0) {
      groups.push({ kind: "section", section, fields: sectionFields });
    }
  }
  return groups;
}

export function intakeFieldGroupTrack(group: IntakeFieldGroup): string {
  return group.kind === "section" ? group.section.id : "default";
}
