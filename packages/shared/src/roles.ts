export const AdeniRoles = {
  Customer: "customer",
  Business: "business",
  Admin: "admin",
} as const;

export type AdeniRole = (typeof AdeniRoles)[keyof typeof AdeniRoles];
