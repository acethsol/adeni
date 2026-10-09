# Adeni product direction (V1 authority)

> **Status:** Canonical for agents and engineers | **Supersedes** conflicting older notes when they disagree with this doc or `packages/brand/specification/MASTER_SPEC.md`.

## What we are building

**Adeni Beauty & Wellness** for **Ottawa, Canada** and **Lagos, Nigeria** — not a multi-industry local-services marketplace in V1.

Initial business types include spas, massage/bodywork, hair/barber, nails, non-medical skincare/aesthetics, yoga/Pilates, personal training/fitness studios, and wellness/recovery. Avoid clinical/medical workflows in V1 unless explicitly requested.

**Out of V1:** plumbers, restaurants, contractors, lawyers, photographers, and other unrelated industries.

## Core customer journey

Discover → business profile → services → practitioner (where applicable) → availability → book → deposit/payment → confirmation/reminders → attend → receipt → review → rebook.

## Business experience (portal)

Profile, locations, practitioners/staff, services, pricing, duration, availability, calendar, appointments, customers, messaging, invoices/receipts, deposits/payments, reviews, notifications, basic analytics.

## Stack

| Layer | Choice |
|-------|--------|
| API | .NET modular monolith |
| Web | Angular — Discover (SSR/SEO), Business Portal, Admin |
| Mobile | Flutter — **consumer app is the primary mobile focus**; business mobile follows portal depth |
| Data | SQL Server |
| Realtime messaging | SignalR |
| Cache / locks | Redis where justified |
| Cloud | Azure |

Do **not** introduce microservices without concrete scaling, deployment, compliance, or ownership reasons.

## Commerce / payments

Adeni does **not** hold customer or business funds in V1. Adeni owns invoice UX, payment requests, checkout, status, receipts, records, reconciliation UX. Licensed providers handle regulated processing and custody. Provider-agnostic abstractions; no raw card storage.

## Brand & UI

- **Option 3 — Premium & Distinctive** ([MASTER_SPEC.md](../packages/brand/specification/MASTER_SPEC.md))
- Use the corrected **PNG** pack in `@adeni/brand` (see `CURSOR_BRAND_INSTRUCTIONS.md`); never redraw, recolor, or CSS-recreate the symbol or wordmark.
- **Carbon Design System** for UI patterns/tokens; Adeni brand is separate — consumer-friendly beauty/wellness polish via imagery, spacing, hierarchy.
- Primary nav lockup: **`adeni-logo.png` / `adeni-logo-dark.png`** via `AdeniBrandLockupComponent` and staff sidebar components.

Tagline *BEAUTY • WELLNESS • YOU* is optional and secondary.

## SEO (Discover)

Public, crawlable URLs toward `/ottawa`, `/lagos`, category/geo pages, `/business/{slug}`, service pages — SSR/prerender, metadata, canonical URLs, structured data, sitemap, Core Web Vitals.

## Globalization

One product for Ottawa and Lagos via configuration: country, city, currency, timezone, locale, tax, verification, payment providers — not separate apps per country.

## Engineering

Inspect before large changes; smallest coherent diff; tests for important behavior; tenant/auth first-class; auditable financial/admin mutations; no speculative infrastructure.

**Related:** [product-strategy.md](./product-strategy.md) (revenue/GTM detail), [architecture.md](./architecture.md) (module map), [AGENTS.md](../AGENTS.md) (agent playbook).
