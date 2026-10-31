# Spec: Business supply landing (“For businesses”)

> Public marketing funnel for prospective owners before Auth0 / portal shell.  
> Standing play 1 — [AGENTS.md](../../AGENTS.md).

| Field | Value |
|-------|-------|
| **Sprint / track** | GTM / supply acquisition (parallel to **33** self-serve billing, **C2** consumer portal) |
| **Author** | Agent + product |
| **Status** | Implemented |
| **Created** | 2026-10-31 |

---

## 1. Goal (one sentence)

Give prospective beauty & wellness businesses a **Fresha-style public landing** (value prop, journey, pricing teaser, clear CTAs) so discover’s **“For businesses”** link converts to **sign-up → `/register`**, not an Auth0 wall or empty dashboard.

---

## Implementation notes (2026-10-31)

- Business portal public route: **`/welcome`** (`WelcomeComponent`).
- **`/setup`** → redirect to **`/welcome`**; guards send unconfigured / dev signed-out users to **`/welcome`**.
- Discover links use **`resolveBusinessPortalWelcomeUrl`** in `@adeni/shared`.
- Pricing: embedded tier cards (NG catalog) + link to discover **`/plan?from=business-welcome`**.

See [frontend.md](../frontend.md) for local URLs.
