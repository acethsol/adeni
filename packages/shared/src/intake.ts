import { z } from "zod";
export const intakeChoiceSchema = z.object({ id: z.string().uuid(), label: z.string().min(1).max(250) }).strict();
export const intakeFieldSchema = z.object({ id: z.string().uuid(), type: z.number().int().min(0).max(6), label: z.string().min(1).max(500), helpText: z.string().max(1000).nullable(), required: z.boolean(), choices: z.array(intakeChoiceSchema).max(20) }).strict();
export const intakeDefinitionSchema = z.object({ title: z.string().min(1).max(200), description: z.string().max(2000).nullable(), instructions: z.string().max(3000).nullable(), fields: z.array(intakeFieldSchema).min(1).max(30) }).strict();
// A newly created blank draft has zero fields; publication/submission require the complete definition schema.
export const intakeDraftSchema = intakeDefinitionSchema.extend({ fields: z.array(intakeFieldSchema).max(30) });
export const intakeAnswerSchema = z.object({ fieldId: z.string().uuid(), text: z.string().max(4000).nullable().optional(), boolean: z.boolean().nullable().optional(), date: z.string().regex(/^\d{4}-\d{2}-\d{2}$/).nullable().optional(), choices: z.array(z.string().uuid()).max(20).nullable().optional() }).strict();
export const intakeTemplateSchema = z.object({ id: z.string().uuid(), isActive: z.boolean(), revision: z.string(), publishedVersionId: z.string().uuid().nullable(), draftVersionId: z.string().uuid().nullable(), draft: intakeDraftSchema.nullable(), published: intakeDefinitionSchema.nullable() });
export const intakeSummarySchema = z.object({ id: z.string().uuid(), name: z.string(), isActive: z.boolean(), publishedVersionId: z.string().uuid().nullable(), revision: z.string() });
export const intakeBindingSchema = z.object({ templateId: z.string().uuid().nullable(), revision: z.string() });
export const intakeStatusSchema = z.object({ id: z.string().uuid(), versionId: z.string().uuid(), state: z.enum(["pending", "completed", "closedUncompleted", "purged"]), submittedAt: z.string().nullable(), purgedAt: z.string().nullable(), canSubmit: z.boolean() });
export const intakeViewSchema = z.object({ assignment: intakeStatusSchema, definition: intakeDefinitionSchema, answers: z.array(intakeAnswerSchema).nullable() });
export type IntakeDefinition = z.infer<typeof intakeDraftSchema>;
export type IntakeField = z.infer<typeof intakeFieldSchema>;
export type IntakeAnswer = z.infer<typeof intakeAnswerSchema>;
export type IntakeTemplate = z.infer<typeof intakeTemplateSchema>;
export type IntakeSummary = z.infer<typeof intakeSummarySchema>;
export type IntakeStatus = z.infer<typeof intakeStatusSchema>;
export type IntakeView = z.infer<typeof intakeViewSchema>;

export const intakeSummariesSchema = intakeSummarySchema.array();
export const intakeStatusesSchema = intakeStatusSchema.array();
export const intakeStatusBatchSchema = z.record(z.string().uuid(), intakeStatusesSchema);
export const intakeViewsSchema = intakeViewSchema.array();
