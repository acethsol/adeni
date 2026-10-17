# Adeni Product Bible (MASTER)

> **Working codename:** Adeni — not final brand identity. Legal/trademark review pending.

---

## What Adeni is

Adeni is the **operating and transaction layer for local service businesses**. Customers discover verified businesses by location, book appointments or request quotes, message providers, and leave reviews. Businesses get structured profiles, workflow-aware portals, and a trust layer that replaces the Google → Instagram → WhatsApp workflow.

**Core insight:** The problem is not discovery alone — it is **trust + coordination**.

**Vision:** Become the global trust layer for local services — enabling anyone to discover, verify, and book services with confidence.

**Slogan:** Trust Every Booking.

---

## Launch focus (locked)

| Dimension | Decision |
| --- | --- |
| Primary market | **Lagos, Nigeria** (v1 pilot) |
| Launch vertical | **Beauty** (barbers, salons, nail/spa) |
| Strategy | **Supply-first** — onboard businesses before scaling demand |
| Architecture | **Modular monolith** — one API, module boundaries ([detail](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649)) |
| Compliance | **SOC 2 Type I** at MVP launch; Type II within 12 months |

Abuja and other Nigerian cities are Phase 2. Quebec City is out of scope for v1.

---

## MVP at a glance

**Shipped (Sprints 0–14):** Signup, discovery, categories, profiles, reviews, booking (request → accept), business portal, manual verification, multi-tenant backend, Expo + Next.js clients, media uploads, tenant isolation hardening.

**Planned (Sprints 15–20):** Business-type workflows, notifications, Paystack payments, SaaS tiers, messaging/WhatsApp, quote flows, staging deploy, LLM agents, modular monolith hardening.

**v1 wedge vs Booksy:** Verified businesses + **workflow-aware tooling** (not appointment-only) — replacing fragmented channels, not salon-tool feature parity.

---

## Locked decisions (v1.1 review, updated Aug 2026)

| # | Decision | Choice |
| --- | --- | --- |
| 1 | Document structure | Bible = this page; Foundation = detailed child page |
| 2 | Payments | Planned Sprint 15/17 (Paystack orchestration); 0% take rate Lagos pilot |
| 3 | Verified badge | In MVP with manual admin verification; tiered badges → Sprint 19 |
| 4 | Launch market | Lagos, Nigeria |
| 5 | Launch vertical | Beauty |
| 6 | User/tenant model | Customers global; business data tenant-scoped; 3 roles |
| 7 | Differentiators | Trust + booking + **business-type workflows** (appointment vs quote) |
| 8 | Compliance | SOC 2 + NDPR from day one |
| 9 | Frontend stack | **Next.js** + **Expo** (ADR-010, July 2026) |
| 10 | Backend architecture | **Modular monolith** (ADR-011, August 2026) |

---

## Documentation index

### Strategy & architecture (Aug 2026)

| Page | Purpose | Status |
| --- | --- | --- |
| [Product strategy & revenue model](https://aceth.atlassian.net/wiki/spaces/SD/pages/41091073) | GTM wedge, revenue model, business types, AI | **Current** |
| [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649) | Module map, boundaries, domain events | **Current** |
| [System Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26509314) | C4 diagrams, deployment, ADRs (v1.2) | Current |
| [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065) | Next.js + Expo monorepo | Current |
| [Observability v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210) | Health checks, SLOs, App Insights | Current |

### Product & compliance

| Page | Purpose | Status |
| --- | --- | --- |
| [Product Foundation v1.1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26411009) | Problem, users, MVP, principles | Current |
| [PRD v1.1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26411033) | Features, user stories, APIs | Current |
| [Data Model (ERD v1)](https://aceth.atlassian.net/wiki/spaces/SD/pages/26574850) | Tables, columns, tenant rules | Complete |
| [User Flows v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26542090) | Customer, business, admin journeys | Current |
| [SOC 2 Compliance Framework v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170) | Trust criteria, controls, NDPR | Current |

### Sprint plan

| Sprint | Page | Status |
| --- | --- | --- |
| 11 | [Discovery UX](https://aceth.atlassian.net/wiki/spaces/SD/pages/28540929) | ✅ Done |
| 12 | [Media & tenant hardening](https://aceth.atlassian.net/wiki/spaces/SD/pages/28540956) | ✅ Done |
| 13 | [Reviews & ratings](https://aceth.atlassian.net/wiki/spaces/SD/pages/28672001) | ✅ Done |
| 14 | [UX polish & guardrails](https://aceth.atlassian.net/wiki/spaces/SD/pages/30769154) | ✅ Done |
| 15 | [Booking v2 + business types](https://aceth.atlassian.net/wiki/spaces/SD/pages/30801921) | Planned |
| 16 | [Business SaaS & monetization](https://aceth.atlassian.net/wiki/spaces/SD/pages/41222145) | Planned |
| 17 | [Commerce orchestration (Paystack)](https://aceth.atlassian.net/wiki/spaces/SD/pages/41287681) | Planned |
| 18 | [Messaging & WhatsApp bridge](https://aceth.atlassian.net/wiki/spaces/SD/pages/41320449) | Planned |
| 19 | [Trust depth & quote workflows](https://aceth.atlassian.net/wiki/spaces/SD/pages/41189385) | Planned |
| 20 | [Deployment, AI, observability & architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/30834689) | Planned |

**Repo mirror:** [acethsol/adeni](https://github.com/acethsol/adeni) — `docs/sprints.md`, `docs/product-strategy.md`, `docs/architecture.md`

---

## Key principles

1. **Trust first** — verification and visible badges are foundational
2. **Not appointment-only** — business types drive different customer journeys
3. **Modular monolith** — one deployable API; extract modules when justified
4. **Simplicity over features** — every feature must reduce friction to booking
5. **Mobile-first UX, web-first discovery** — Expo daily use; Next.js SSR for SEO
6. **Speed to booking** — minimize steps from need → booked
7. **Compliance by design** — SOC 2 controls embedded from Sprint 0

---

## Tech stack (summary)

**Next.js** (public + business + admin) · **Expo** (iOS/Android) · **.NET modular monolith** · **PostgreSQL** · **Redis** · **Auth0** · **Azure Blob** · **App Insights**

See [System Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26509314), [Modular monolith architecture](https://aceth.atlassian.net/wiki/spaces/SD/pages/42139649), [Frontend Architecture v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26968065), [Observability v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/27230210), and [SOC 2 Compliance Framework v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170) for full detail.

---

_Last updated: August 2026 — strategy, modular monolith, sprints 14–20_
