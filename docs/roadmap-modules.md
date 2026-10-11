# Roadmap — product modules

> **Status:** Living module roadmap | **Last updated:** October 2026  
> **Parent:** [product-strategy.md](./product-strategy.md) · [architecture.md](./architecture.md)  
> **Deep dive (money / queue / POS):** [roadmap-workflows-payments-queue.md](./roadmap-workflows-payments-queue.md)  
> **Principle:** Compete without complexity — ship thin modules that close the floor journey; defer ERP depth.

This doc is the **module index**: what exists, what’s next, and how each area maps to day-in-the-life and competitors. Detailed payment/queue/POS decisions live in the deep-dive doc above.

---

## 1. How to read this

| Column | Meaning |
|--------|---------|
| **Status** | `Shipped` · `Partial` · `Planned` · `Later` · `Out of scope (early)` |
| **Horizon** | `Now` · `Next` · `Then` · `Later` |
| **Code module** | Domain / Application home when it exists |
| **Owner** | Product owner seat (fill as team grows) |

Each major build still needs a `docs/specs/sprint-XX-*.md` before implementation.

---

## 2. Module index

| # | Module | Status | Horizon | Code / docs | Notes |
|---|--------|--------|---------|-------------|-------|
| 1 | **Queue & check-in** | Planned | Then | Booking + new queue capability | Walk-in + kiosk; see deep dive §5 |
| 2 | **Payments & finance** | Partial | Now → Next | `Payments`, [payments.md](./payments.md) | Orchestration yes; payroll no |
| 2b | **POS bridge** | Planned | Later | Payments adapter | Integrate, don’t build POS |
| 3 | **Verification / trust** | Partial | Now | Tenancy + Admin | Tiers shipped; deepen CAC/automation |
| 4 | **Subscriptions & add-ons** | Partial | Next | Subscriptions | Entitlements shipped; live billing stub |
| 5 | **Dashboards & reporting** | Partial | Next | Portal overview + Admin | Owner “today” first; ops GMV later |
| 6 | **Messaging** | Partial | Next | Messaging | Client↔business done; staff↔staff gap |
| 7 | **Floor calendar & staff** | Shipped | Now (polish) | Booking + Identity | Team + practitioner boards |
| 8 | **Onboarding** | Shipped | Now (polish) | Tenancy | Supply-first; category-aware defaults |
| 9 | **Discovery & public page** | Shipped | Now | Discovery | SEO + shareable book link |
| 10 | **Quotes** | Shipped | Later (wedge) | Quotes | Outside beauty Explore; keep for trades |
| 11 | **AI agents** | Planned | Later | Sprint 20 | Customer + business agents |
| 12 | **Notifications / reminders** | Partial | Next | Notifications | Prefs exist; delivery adapters thin |
| — | **Payroll / full POS / inventory** | Out of scope (early) | Much later | — | Phase 4+; see strategy §8 |

---

## 3. Module briefs

### 3.1 Queue & check-in

| | |
|--|--|
| **Job** | Walk-ins and “I’m here” without a second OS |
| **Surfaces** | Customer join link · staff Next board · tablet **kiosk mode** |
| **States** | Waiting → Called → In service → Done / No-show |
| **Ties to** | Same floor board as appointments; then payment loop |
| **Spec path** | Deep dive → then `sprint-XX-queue-checkin.md` |
| **Owner** | TBD |

### 3.2 Payments, finance & POS bridge

| | |
|--|--|
| **Job** | Close money loop without becoming a bank or a register |
| **Shipped** | Paystack orchestration, deposits, pay links, ledger, stub subscriptions |
| **Next** | [Sprint 27 payment loop](./specs/sprint-27-payment-loop.md) + lightweight receipt; then SaaS Billing |
| **Shipped (26)** | Stripe + Paystack + `MarketDefault` behind `IPaymentProvider` |
| **Then** | Terminal / collect balance; **POS bridge** (Square first) |
| **Later fintech** | Tips/commission *reporting*, payout UX referral, Insights tier — still non-custodial |
| **Not now** | Payroll, wallets, retail SKU catalog |
| **Owner** | TBD |

### 3.3 Verification

| | |
|--|--|
| **Job** | Trust Every Booking — badges customers can believe |
| **Shipped** | Admin approve, tiered badges, category license needs, review queue |
| **Next** | Clearer upgrade UX, Ottawa trust signals, CAC automation depth |
| **Never** | Pay-to-verify |
| **Owner** | TBD |

### 3.4 Subscriptions, pricing & add-ons

| | |
|--|--|
| **Job** | MRR from operating tools + optional add-ons |
| **Shipped** | Free / Pro / Business entitlements + gating |
| **Next** | Real Paystack + Stripe Billing; signed subscription webhooks |
| **Add-on candidates** | AI business agent · advanced verification · queue · Insights |
| **Owner** | TBD |

### 3.5 Dashboards & reporting

| | |
|--|--|
| **Job** | “How is my floor today?” — not a BI suite |
| **V1 owner metrics** | Pending, today’s board, hours worked, requested-staff rate, GMV when payments live, repeat clients |
| **Ops (Adeni admin)** | GMV / MRR / active businesses (Sprint 20e) |
| **Defer** | Full accounting, payroll reports, Zenoti-style analytics packs |
| **Owner** | TBD |

### 3.6 Messaging

| | |
|--|--|
| **Job** | Meet customers where they are; light staff handoff |
| **Shipped** | Client ↔ business threads, WhatsApp deep links, templates, Pro gate |
| **Gap** | Staff ↔ staff / front-desk notes on bookings |
| **Later** | SignalR realtime, WABA |
| **Owner** | TBD |

### 3.7 Floor calendar & staff (reference)

| | |
|--|--|
| **Job** | Owner team board + practitioner My calendar |
| **Shipped** | Team calendar, hours rollups, customer-requested badge, staff invites & roles |
| **Polish** | Mobile parity, photo E2E, extract board to `@adeni/ui` when needed |
| **Owner** | TBD |

### 3.8 Onboarding

| | |
|--|--|
| **Job** | Business live with a shareable book link before marketplace traffic |
| **Shipped** | Register → services/hours → verification → Verified → public page |
| **Polish** | Category-aware setup, service templates, invite first staff, optional Pro trial |
| **Owner** | TBD |

---

## 4. Day-in-the-life (module coverage)

### 4.1 Nail salon owner

| Moment | Modules |
|--------|---------|
| Morning: pending + today’s board | Dashboards, Calendar, Bookings |
| Staff load / leave / requested clients | Calendar, Staff |
| Walk-ins between appointments | **Queue** (planned) |
| “Can I move to 4?” | Messaging / WhatsApp |
| Collect balance / till | **Payments loop** · optional **POS bridge** |
| End of day numbers | Dashboards (GMV, hours) |

### 4.2 Massage practitioner (staff login)

| Moment | Modules |
|--------|---------|
| Open My calendar only | Calendar (practitioner scope) |
| Own sessions + leave | Staff self, Bookings self |
| Preferred-client badge | Calendar (`customerSelectedStaff`) |
| Client running late | Messaging (own threads) |
| No roster / plan / team calendar | Permissions (Sprint 25) |

### 4.3 New business onboarding

| Step | Modules |
|------|---------|
| Register + category | Onboarding, Catalog |
| Services from templates | Catalog, Booking |
| Hours + location | Booking, Tenancy |
| Verification submit | Verification |
| Invite Ada | Staff access |
| Share Instagram book link | Discovery / public page |
| Optional Pro when messaging matters | Subscriptions |

---

## 5. Competitor lens (module-shaped)

| Competitor | They win on | Adeni module answer |
|------------|-------------|---------------------|
| **Zenoti** | Deep spa OS (queue, memberships, POS, payroll) | Thin queue + payment loop + POS *bridge*; no ERP |
| **Fresha / Booksy** | Consumer density, appointments | Trust + dual city + floor OS before traffic |
| **Mindbody** | Classes / fitness ecosystem | Appointment-first; class UX later |
| **Fragmentation** (IG + WA + transfer) | Habit | Public page + book + pay link + message |

**Honest gaps:** queue, live SaaS checkout, Stripe, deep analytics, staff chat, reliable reminders, AI agents, POS bridge.  
**Moat path:** trust + action + simple floor + non-custodial money.

---

## 6. Sequencing (modules → slices)

```
Now     Calendar/staff polish · verification UX · owner today dashboard
Next    Stripe + SaaS billing · payment loop states · reminders · staff handoff notes
Then    Queue v1 + check-in · kiosk mode
Later   POS bridge · Terminal collect · Insights · AI agents
Avoid   Payroll · full POS · inventory · per-category OS forks
```

Aligned detail: [roadmap-workflows-payments-queue.md §6](./roadmap-workflows-payments-queue.md#6-sequencing).

---

## 7. Ownership board (fill in)

| Module | Product | Eng | Design |
|--------|---------|-----|--------|
| Queue & check-in | | | |
| Payments / POS bridge | | | |
| Verification | | | |
| Subscriptions | | | |
| Dashboards | | | |
| Messaging (staff handoff) | | | |
| Onboarding polish | | | |

---

## 8. Related documents

| Doc | Purpose |
|-----|---------|
| [roadmap-workflows-payments-queue.md](./roadmap-workflows-payments-queue.md) | Decisions: workflows, Paystack+Stripe, loop, queue, POS |
| [product-strategy.md](./product-strategy.md) | Vision & revenue layers |
| [architecture.md](./architecture.md) | Code module map & boundaries |
| [sprints.md](./sprints.md) | Sprint execution |
| [beauty-wellness-taxonomy.md](./beauty-wellness-taxonomy.md) | Categories |
| [subscription-billing.md](./subscription-billing.md) | SaaS billing stub |
| [specs/sprint-18-messaging.md](./specs/sprint-18-messaging.md) | Messaging shipped scope |
| [specs/sprint-25-staff-accounts-permissions.md](./specs/sprint-25-staff-accounts-permissions.md) | Staff roles |
