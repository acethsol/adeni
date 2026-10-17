# Frontend — Shared packages

> **Parent:** [Frontend Overview](../frontend-overview.md)

---

## Purpose

**Single contract layer** between Angular apps and the .NET API — types, validation, HTTP client, and reusable staff UI.

---

## Package matrix

| Package | npm name | Contains | Consumers |
|---------|----------|----------|-----------|
| Shared | `@adeni/shared` | Zod schemas, roles, capabilities, markets, formatters, i18n error keys, wellness JSON | api-client, all apps |
| API client | `@adeni/api-client` | Typed fetch methods, parses with Zod | All Angular apps |
| UI | `@adeni/ui` | Portal/admin Angular components, staff SCSS | portal, admin |
| Brand | `@adeni/brand` | Logo PNGs, color tokens, path helpers | ui, apps (via ui) |

---

## Dependency rules

```
apps/*  →  @adeni/ui (optional)  →  @adeni/api-client  →  @adeni/shared
```

- **Never** import Angular from `@adeni/shared`.
- **Never** put HTTP fetch in `shared` — belongs in `api-client`.
- When API changes: update **Domain/DTO → shared Zod → api-client → apps**.

---

## `@adeni/shared` highlights

| Area | Path (under `packages/shared/src`) |
|------|-------------------------------------|
| Schemas | `schemas/*` — booking, discovery, admin, payments, … |
| Product constants | `business-types`, `capabilities`, `subscription-tiers` |
| Markets | `markets.ts`, `data/wellness-categories.json` |
| Errors | `api-errors`, `i18n/errors.ts` |

Run tests: `npm run test --workspace @adeni/shared`

---

## `@adeni/api-client`

- Single entry `src/index.ts` exporting domain-grouped functions.
- Attaches correlation id, auth headers (configured per app).
- Validates responses with Zod schemas from shared.

Run typecheck: `npm run typecheck --workspace @adeni/api-client`

---

## `@adeni/ui`

Shared **staff** presentation:

- Page chrome (`PortalPageComponent` pattern)
- Staff buttons, brand logo component
- Layout SCSS shared between portal and admin

Discover uses fewer ui imports — consumer-specific components stay in `apps/discover`.

See `packages/ui/README.md`.

---

## `@adeni/brand`

- Option 3 corrected PNG identity pack
- `sync:brand` postinstall script at repo root
- Spec: `packages/brand/specification/MASTER_SPEC.md`

---

## Workspace configuration

Root `package.json` workspaces:

- `apps/discover`, `apps/portal`, `apps/admin`
- `packages/*`

Node: see `.nvmrc` (22.x). Angular versions pinned via root `overrides`.

---

## Flutter note

Mobile does not consume these npm packages. Keep behavioral parity by generating clients from OpenAPI or manual sync when contracts change.

---

## Related docs

- `AGENTS.md` § Shared packages
- [Backend Overview](../backend-overview.md) § HTTP conventions
