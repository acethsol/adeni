import { z } from "zod";

export const customerVerificationResponseSchema = z.object({
  emailVerified: z.boolean(),
  phoneVerified: z.boolean(),
  emailVerifiedAt: z.string().nullable(),
  phoneVerifiedAt: z.string().nullable(),
});

export type CustomerVerificationResponse = z.infer<typeof customerVerificationResponseSchema>;

export const customerProfileTrustSchema = z.object({
  phoneVerified: z.boolean(),
  customerSince: z.string(),
});

export type CustomerProfileTrust = z.infer<typeof customerProfileTrustSchema>;
