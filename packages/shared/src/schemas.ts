import { z } from "zod";

export const categorySchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  parentSlug: z.string().nullable().optional(),
});

export const categoriesResponseSchema = z.object({
  items: z.array(categorySchema),
});

export type Category = z.infer<typeof categorySchema>;

export const discoveryBusinessItemSchema = z.object({
  locationId: z.string(),
  tenantId: z.string(),
  name: z.string(),
  locationName: z.string(),
  slug: z.string(),
  categorySlug: z.string(),
  area: z.string(),
  marketId: z.string(),
  distanceKm: z.number(),
  latitude: z.number(),
  longitude: z.number(),
});

export const discoveryResponseSchema = z.object({
  items: z.array(discoveryBusinessItemSchema),
  page: z.number(),
  pageSize: z.number(),
  totalCount: z.number(),
});

export type DiscoveryBusinessItem = z.infer<typeof discoveryBusinessItemSchema>;
export type DiscoveryResponse = z.infer<typeof discoveryResponseSchema>;

export const publicBusinessProfileSchema = z.object({
  locationId: z.string(),
  tenantId: z.string(),
  name: z.string(),
  locationName: z.string(),
  slug: z.string(),
  categorySlug: z.string(),
  area: z.string(),
  marketId: z.string(),
  addressLine: z.string(),
  description: z.string(),
  phoneMasked: z.string(),
  latitude: z.number().nullable(),
  longitude: z.number().nullable(),
});

export type PublicBusinessProfile = z.infer<typeof publicBusinessProfileSchema>;

export const authSessionSchema = z.object({
  userId: z.string().nullable(),
  roles: z.array(z.string()),
  tenantId: z.string().uuid().nullable(),
  hasMfa: z.boolean(),
});

export type AuthSession = z.infer<typeof authSessionSchema>;

export const pendingBusinessSchema = z.object({
  id: z.string(),
  name: z.string(),
  slug: z.string(),
  marketId: z.string(),
  status: z.string(),
  createdAt: z.string(),
});

export const pendingBusinessesResponseSchema = z.object({
  items: z.array(pendingBusinessSchema),
});

export type PendingBusiness = z.infer<typeof pendingBusinessSchema>;