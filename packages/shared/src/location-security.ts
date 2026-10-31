import { z } from "zod";

export const locationSecurityPolicyResponseSchema = z.object({
  requirePresenceForSensitiveCustomerData: z.boolean(),
  presenceValidityMinutes: z.number().int(),
  allowManagerPresenceOverride: z.boolean(),
  policyConfigurable: z.boolean(),
  enforcementActive: z.boolean(),
});

export const updateLocationSecurityPolicyRequestSchema = z.object({
  requirePresenceForSensitiveCustomerData: z.boolean(),
  presenceValidityMinutes: z.number().int().min(30).max(180).optional(),
  allowManagerPresenceOverride: z.boolean().optional(),
});

export type LocationSecurityPolicyResponse = z.infer<typeof locationSecurityPolicyResponseSchema>;
export type UpdateLocationSecurityPolicyRequest = z.infer<typeof updateLocationSecurityPolicyRequestSchema>;
