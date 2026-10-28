# Spec: Intake form sections

| Field | Value |
|-------|-------|
| **Sprint** | F2 extension — consultation form UX |
| **Author** | Agent |
| **Status** | Implemented |
| **Created** | 2026-10-28 |

---

## 1. Goal (one sentence)

Let businesses group consultation form fields into named sections (Personal info, Health history, etc.) so guests and staff see structured forms instead of one flat scroll, without changing submission or versioning rules.

---

## 2. Context

- Builds on [f2-intake-consultation-forms-closeout.md](./f2-intake-consultation-forms-closeout.md)
- Module: **Intake** (templates, published definitions, guest submit, portal review)
- No workflow engine, attachments, or e-signatures

---

## 3. In scope

- [ ] Optional `sections` on draft/published definition: `{ id, title, description?, sortOrder }[]` (max 10)
- [ ] Optional `sectionId` on each field (`null` = top/default block before sectioned content)
- [ ] Server validation on draft save + publish (bounds, unique section ids, field references)
- [ ] Persist sections in new `intake.form_sections`; `SectionId` nullable FK on `intake.form_fields`
- [ ] Render section headings in `@adeni/ui` `IntakeFieldsComponent` (guest, preview, read-only review)
- [ ] Portal intake editor: add/rename/reorder/remove sections; assign fields to sections
- [ ] Backward compatible: definitions without `sections` behave as today
- [ ] Wellness consultation seed JSON + SQL example with four sections
- [ ] Tests: shared Zod, `IntakeValidation`, field component section headings

## 4. Out of scope

- Multi-page wizard / stepper with separate URLs
- Conditional logic (“show field if…”)
- Attachments, signatures, PDF export layout
- Per-section completion or progress persistence
- Entitlement-gated section counts
- Migrating existing published forms automatically (owners republish via draft clone)

---

## 5. API contract

No new routes. Existing tenant intake draft PUT and published definition payloads gain optional properties.

**Definition shape** (`packages/shared`, mirrors Application `IntakeDefinition`):

```json
{
  "title": "Body Sculpt wellness consultation",
  "description": null,
  "instructions": null,
  "sections": [
    {
      "id": "b1000001-0001-4001-8001-000000000001",
      "title": "Personal information",
      "description": null,
      "sortOrder": 0
    }
  ],
  "fields": [
    {
      "id": "a1000101-0001-4001-8001-000000000001",
      "type": 0,
      "label": "Full name",
      "helpText": null,
      "required": true,
      "choices": [],
      "sectionId": "b1000001-0001-4001-8001-000000000001"
    }
  ]
}
```

| Rule | Limit |
|------|-------|
| Sections | 0–10; unique ids; title 1–200; description ≤1000; sortOrder ≥0 |
| Fields | Still 1–30; `sectionId` must be null or reference a section on the same definition |
| Answers | Unchanged — keyed by `fieldId` only |

---

## 6. Files to touch

| Layer | Files |
|-------|-------|
| Domain | `IntakeModels.cs` (`IntakeFormSection`, `SectionId` on field) |
| Application | `IntakeValidation.cs` (records + validation) |
| Infrastructure | `IntakeModelConfiguration.cs`, `IntakeDefinitions.cs`, `IntakeTemplateService.cs` |
| Database | `intake/Tables/form_sections.sql`, `form_fields.sql` (+ seed scripts) |
| Tests | `IntakeValidationTests.cs`, `IntakeSqlServerTests.cs`, `intake.test.ts`, portal/ui specs |
| Frontend | `packages/shared/src/intake.ts`, `intake-layout.ts`, i18n overlays |
| UI | `intake-fields.component.ts/scss` |
| Portal | `intake-manager.component.ts/html/scss` |

---

## 7. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Sections scoped by `TenantId` + `VersionId` like fields |
| **Auth / roles** | Unchanged — template mutations owner/manager only |
| **Cache** | None |
| **Audit** | Existing template change audit |
| **PII** | No change — answers unchanged |
| **Observability** | None |

---

## 8. Tests

- [ ] Unit: `IntakeValidation` sections + invalid `sectionId`
- [ ] Unit: shared Zod accepts sections; rejects orphan `sectionId`
- [ ] Component: section heading visible when `sections` present
- [ ] Integration: existing intake HTTP tests still pass (in-memory DB)

Run: `dotnet test Adeni.slnx -c Release`; portal `npm run typecheck` (or project script)

---

## 9. Acceptance criteria

- [ ] Legacy flat forms load and submit unchanged
- [ ] Published preview and Discover guest form show section titles
- [ ] Editor can manage sections and assign fields
- [ ] Wellness seed demonstrates four PDF-like sections
- [ ] DACPAC includes `form_sections` and nullable `SectionId`

---

## 10. Open questions

| Question | Decision |
|----------|----------|
| JSON blob on version vs table | Normalized `form_sections` table (no version JSON column exists) |
| Empty sections in UI | Hide section block when no fields assigned |
| Field order | Global `fields` array order; groups render top unsectioned, then each section by `sortOrder` |
