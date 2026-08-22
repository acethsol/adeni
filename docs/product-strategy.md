# Adeni — Product strategy & revenue model

> **Status:** Living strategy document | **Last updated:** August 2026  
> **Sources:** [ChatGPT strategy research (Jul–Aug 2026)](https://chatgpt.com/share/6a8779af-99d8-83ea-a72d-7de7e8873a1d), codebase audit, sprint history  
> **Parent:** [Adeni Product Bible](https://aceth.atlassian.net/wiki/spaces/SD/pages/26279937) | **Confluence:** [Product strategy & revenue model](https://aceth.atlassian.net/wiki/spaces/SD/pages/41091073) | **Build spec:** [prd-v1.1-body.md](./prd-v1.1-body.md)

---

## 1. What Adeni is (and is not)

### Positioning

Adeni is **not** “another Booksy” or “Nigerian Yelp.”

It is the **operating and transaction layer for local service businesses**:

| Layer | What it means |
|-------|----------------|
| **Discovery** | Find verified businesses near you (geo + category + search) |
| **Trust** | Verification, reviews, transparent badges — not pay-to-verify |
| **Action** | Book, pay, message, get reminders — one journey, not five apps |
| **Business OS** | Profile, services, calendar, inbox, analytics — value even before marketplace traffic |

The real competitor is **fragmentation**: Google → Instagram → WhatsApp → bank transfer → save contact manually. Adeni replaces that stack for businesses that opt in.

### Vision

> To become the world's most trusted platform for discovering, booking, and growing local service businesses.

### Mission

> We empower local businesses with powerful digital tools while helping customers connect with trusted professionals quickly, safely, and confidently.

### Slogan (working)

> **Trust Every Booking.**

### Brand pillars

Every feature should reinforce at least one pillar:

| Pillar | Examples |
|--------|----------|
| **Trust** | Verification tiers, reviews, identity, safety |
| **Convenience** | Booking, payments, messaging, directions |
| **Discovery** | Search, maps, Ask Adeni, recommendations |
| **Growth** | Dashboard, analytics, retention, marketing tools |
| **Community** | Support local businesses; fair marketplace |

---

## 2. Go-to-market wedge (Nigeria first, global architecture)

ChatGPT research and market reality align on the same playbook:

1. **One city** — Lagos (already configured: `lagos` market, seeded businesses).
2. **One vertical** — Beauty & grooming (barbers, salons, nails) — high WhatsApp volume, repeat bookings, price transparency.
3. **Supply-first** — Personally onboard 100–300 businesses free; give them a **mini website + booking link** they can paste in Instagram bio and WhatsApp status.
4. **Demand follows** — Reviews + “Book on Adeni” links create consumer pull; don't wait for organic discovery.
5. **Expand vertically** — Home services → Healthcare → Professional — only after one wedge shows engagement.

### North-star metric (early)

**GMV per active business** — not downloads, not signups.

A business that processes real bookings (and eventually payments) through Adeni is a business we can monetize. Everything else is vanity.

### Chicken-and-egg mitigation

Businesses get value **before** Adeni sends customers:

- Public profile at `/businesses/{slug}` (SEO + shareable link)
- Booking calendar + availability
- Booking inbox
- Reviews display
- *(Planned)* Payment links usable on WhatsApp without marketplace discovery

This is why the **business portal** is strategically as important as the consumer app.

---

## 3. Competitive differentiation

| Idea | Status in codebase | Priority |
|------|-------------------|----------|
| Tiered verification (phone, CAC, address, …) | Basic admin approve/reject only | High — Sprint 19 |
| Reviews tied to completed bookings | ✅ Done (Sprint 13) | — |
| AI concierge (“need a barber in Lekki tomorrow”) | Rule-based Ask Adeni only | Sprint 20 (LLM agent) |
| Mini website per business | ✅ Public profile + slug | Enhance share/SEO |
| Online payments / deposits | Not started | Sprint 15 + 17 |
| Appointment reminders (SMS/WhatsApp/push) | Not started | Sprint 15 |
| In-app messaging | Not started | Sprint 18 |
| Instant quotes (reverse marketplace) | Not started | Sprint 19 (on top of Sprint 15 framework) |
| **Business-type workflows** | One appointment flow for all categories | **High — Sprint 15 foundation, Sprint 19 depth** |
| Digital queue | Future | Backlog |
| Loyalty programs | Future | Backlog |

**Do not compete on:** more reviews, bigger directory, better map than Google.

**Compete on:** trust + action + business tooling — the full journey from “I need X” to “booked and paid.”

### 3.5 Business types & workflow capabilities

Adeni is **not appointment-native** like Booksy. Different local service businesses operate differently — a barber sells fixed-duration slots; a plumber prices jobs after seeing the problem; a cleaner may sell recurring visits. The platform must support **multiple operating models** behind one discovery layer.

#### Three layers (do not conflate)

| Layer | Purpose | Example |
|-------|---------|---------|
| **Discovery category** | What customers browse and filter by | `barbers`, `plumbers`, `cleaning` |
| **Business type** | How the business operates (operating model) | `scheduled_appointment`, `quote_request`, `walk_in_queue` |
| **Workflow capabilities** | Features enabled for that type | calendar, quotes, deposits, on-site address, photo upload, license badges |

Today the codebase has **categories only** (`CategoryService` + tenant `categorySlug`). Every tenant runs the same **scheduled appointment** workflow regardless of category. The business-type layer is the missing platform primitive.

#### Business types (v1 + backlog)

| Business type | Primary customer journey | Portal emphasis | v1? |
|---------------|-------------------------|-----------------|-----|
| **`scheduled_appointment`** | Pick service → pick slot → confirm | Calendar, services, availability, inbox | ✅ Yes — Lagos wedge |
| **`quote_request`** | Describe job → upload photos → receive quote → accept → pay deposit | Quote inbox, job details, deposit settings | ✅ Yes — home services expansion |
| **`walk_in_queue`** | See wait time → join queue → notified when ready | Queue board, walk-in toggle | Backlog |
| **`recurring_service`** | Pick plan → schedule recurring visits | Recurrence rules, service areas | Backlog |
| **`on_demand_dispatch`** | Request now → matched to available pro | Dispatch board, ETA | Backlog |

**v1 ships two types:** `scheduled_appointment` + `quote_request`. Everything else stays in config as disabled capabilities until justified by vertical expansion.

#### Category → default business type (current catalog)

| Category | Group | Default business type | Default capabilities |
|----------|-------|----------------------|----------------------|
| `barbers` | beauty | `scheduled_appointment` | calendar, fixed pricing, reviews, walk-in flag (future) |
| `hair-salons` | beauty | `scheduled_appointment` | calendar, fixed pricing, multi-service |
| `nail-spa` | beauty | `scheduled_appointment` | calendar, fixed pricing |
| `makeup-brows` | beauty | `scheduled_appointment` | calendar, fixed pricing |
| `plumbers` | home-services | `quote_request` | quotes, on-site address, photo upload, license badge |
| `electricians` | home-services | `quote_request` | quotes, on-site address, photo upload, license badge |
| `cleaning` | home-services | `scheduled_appointment` | calendar, fixed pricing, service area (recurring later) |

Tenants inherit the default for their category at onboarding. Override allowed only within capabilities permitted for that category (e.g. a plumber cannot switch to pure walk-in queue until that type is enabled).

#### Capability matrix (drives UX + API)

| Capability | Barbers / salons | Plumbers / electricians | Cleaning |
|------------|------------------|-------------------------|----------|
| Slot calendar | ✅ primary | optional | ✅ primary |
| Quote / job request flow | ❌ | ✅ primary | optional |
| Fixed service pricing | ✅ | ❌ (quote-based) | ✅ |
| Deposit at confirm | optional | ✅ | optional |
| Customer on-site address | ❌ | ✅ | ✅ |
| Photo upload on request | ❌ | ✅ | optional |
| License verification badge | ❌ | ✅ | ❌ |
| Discovery CTA | “Book now” | “Get a quote” | “Book now” |

Capabilities are returned by the API (`GET /tenant/profile` or dedicated `capabilities` field) so web, mobile, and future AI agents branch consistently — no hard-coded category checks in UI.

#### Why this is strategic

1. **Onboarding adapts** — barber sees calendar setup; plumber sees “how do you price jobs?”
2. **Discovery cards differ** — “Book now” vs “Get a quote” CTAs per business type
3. **Portal nav hides noise** — quote businesses don't get calendar-first UX
4. **Verification follows category** — license badge for trades; basic for beauty
5. **AI agent (Sprint 20c) reads type** — hours/availability FAQ vs “send photos of the issue”

#### Engineering model

```
Category (discovery taxonomy)
    └── defaultBusinessType
            └── capabilities[]     ← drives portal modules + customer journey
                    └── services[] ← pricingType per service (fixed | quote | hourly)
```

- **`BusinessType`** enum on tenant profile (set at onboarding, default from category)
- **`capabilities`** config in `@adeni/shared` + API — single source of truth
- **Customer journey router** in web/mobile — branch on `businessType`, not category slug
- **Sprint 15** lays the foundation; **Sprint 19** delivers full quote workflow on top

**Implementation:** Sprint 15 (**15a** architecture → **15h–15k** business types) → Sprint 19 (quote depth + category verification) → Sprint 20c (type-aware business AI).

---

## 4. Revenue model

Adeni is a **hybrid platform**: predictable SaaS (MRR/ARR) + transaction upside (GMV × take rate) + orchestrated fintech (no custody).

### 4.1 SaaS subscriptions — foundation (MRR/ARR)

Businesses pay monthly for operating tools.

| Tier | Indicative price | Includes |
|------|------------------|----------|
| **Free** | ₦0 | Profile, services, limited bookings/month, basic calendar |
| **Pro** | ~₦5k–15k/mo | Unlimited bookings, messaging, reminders, analytics, AI add-on |
| **Business** | ~₦25k+/mo | Multi-location, staff, priority support, advanced verification |

Example scale (illustrative): 10,000 paying × $30/mo ≈ **$300k MRR / $3.6M ARR** before any transaction revenue.

**Implementation:** entitlements model in API + portal gating → Sprint 16.

### 4.2 Marketplace / transaction fees (GMV × take rate)

When customers pay through Adeni:

- Track **GMV** (gross merchandise value)
- Apply **take rate** (e.g. 1–3% platform fee on top of payment provider costs)

Example: $20M monthly GMV × 2% = **$400k/month**.

Requires payments in flow → Sprint 15 (deposits) + Sprint 17 (orchestration).

### 4.3 Fintech — orchestration, not custody

**Strategic decision (Aug 2026):** Adeni does **not** hold customer or business funds. Licensed payment providers handle money movement; Adeni owns the commerce UX and earns from:

| Stream | Mechanism |
|--------|-----------|
| Payment referral / rev-share | Commercial agreement with Paystack (NG) / Stripe (global) |
| Platform transaction fee | Small Adeni fee per facilitated payment |
| Payment links & invoices | Businesses send “Pay now” links via WhatsApp — works **without** marketplace traffic |
| Instant payout referral | Surface provider’s instant payout; rev-share |
| Financing / insurance referrals | Later — use Adeni transaction history as underwriting signal |
| Financial analytics SaaS | “Adeni Insights” tier — revenue, repeat rate, outstanding invoices |

**Highest near-term ROI:** payment links + booking deposits via WhatsApp. A barber can share an Adeni link in chat today even if zero consumers use the discovery app.

**Regulatory posture:** “Adeni provides software integrated with licensed payment providers” — not a wallet, bank, or money transmitter.

### 4.4 Other streams (later)

| Stream | Notes |
|--------|-------|
| Featured listings / ads | Only after traffic; never compromises verification |
| Premium verification checks | Charge for *advanced* checks (CAC lookup, site visit), not basic trust badge |
| AI assistant subscription | Business agent auto-replies → high-margin SaaS |
| API / enterprise | Hotels, property managers, insurers accessing verified provider network |

### 4.5 Four economic layers (maturity model)

```
Layer 1 — SaaS          → MRR / ARR
Layer 2 — Marketplace   → GMV × take rate
Layer 3 — Fintech       → Payment volume × orchestration economics
Layer 4 — Data / AI     → Subscriptions + usage
```

### 4.6 Sequencing (agreed)

| Phase | Focus |
|-------|-------|
| **1 — Now** | Discovery + booking + business SaaS + trust |
| **2** | Payments (deposits, pay links, receipts) via providers |
| **3** | Payouts, reconciliation, recurring billing, financial reporting |
| **4** | Fintech ecosystem (referrals, instant payout UX, financing partners) |
| **5** | AI agents (customer + business) with tool-calling on Adeni APIs |

**Do not start with fintech custody.** Complexity (KYC/AML, chargebacks, licensing) kills early velocity.

---

## 5. Product evolution timeline

| Year | Adeni becomes… |
|------|----------------|
| 2026 | Directory + discovery + booking + verified profiles |
| 2027–28 | Marketplace + business SaaS + payments orchestration |
| 2029–30 | + AI agents + financial analytics |
| 2030+ | Infrastructure connecting consumers, businesses, and AI agents to local services |

The interface may change (voice, AI assistants). The problem — trusted local service commerce — does not.

---

## 6. AI agent strategy

Two agents, one platform:

### Customer agent

> “Barber in Lekki tomorrow after 5pm, under ₦20k, highly rated.”

→ search → filter → present → confirm → book (with permission gates).

### Business agent (paid feature)

Auto-responds to inquiries using business hours, services, prices, availability. High value for solo operators mid-haircut.

**Architecture:** LLM + **tool-calling** against existing Adeni APIs — never direct DB access. Read-only tools automatic; booking/payment/cancel require confirmation.

**Build path:** Rule-based Ask Adeni (✅ Sprint 11) → LLM agent with tools (Sprint 20) → Business agent (post-20).

**Proprietary moat:** Adeni's structured data (services, availability, reviews, transaction history) — not the underlying model.

---

## 7. Technical strategy (engineering input)

This section captures **Cursor/codebase reality**, which diverges from early ChatGPT assumptions in healthy ways.

### 7.1 Stack — what we actually built

| ChatGPT early advice | Current reality | Verdict |
|---------------------|-----------------|---------|
| Flutter single codebase | Next.js web + Expo mobile monorepo (`packages/shared`, `@adeni/api-client`) | ✅ Correct pivot (ADR-010). SEO web + native mobile beats Flutter web for discovery. |
| NativeScript mobile | Expo + React Native | ✅ Upgraded |
| Auth0 | Auth0 (web + mobile) | ✅ As planned |
| .NET + PostgreSQL + Redis | .NET + PostgreSQL + Redis | ✅ As planned |

### 7.2 Architecture — modular monolith, not microservices

**Canonical doc:** [architecture.md](./architecture.md)

ChatGPT's later advice matches engineering best practice for a solo/small team. Adeni is **one deployable .NET API** with vertical modules inside Clean Architecture layers — not a microservice mesh.

| Rule | Detail |
|------|--------|
| **One deployable unit** | Single `Adeni.Api` + PostgreSQL; one release train |
| **Module boundaries** | Tenancy, Booking, Discovery, Reviews, Catalog, Admin, Storage (+ Payments, Messaging, Notifications as they land) |
| **Cross-module calls** | Application interfaces (`I*Service`) only — never reach into another module's Infrastructure |
| **Domain events** | `BookingConfirmed`, `ReviewSubmitted`, … — in-process now, bus when a module extracts |
| **Design for extraction** | Search, Notifications, Messaging, Payments, AI are the first split candidates — only when scale/team/compliance justify it |
| **Frontend contract** | `packages/shared` + `api-client` mirror API modules; UI branches on capabilities, not ad-hoc category checks |

**Current state:** folder-based modules exist in Domain/Application/Infrastructure; flat DI registration; no domain events yet. **Sprint 15a** hardens boundaries first (per-module DI, event dispatcher, architecture tests) — before Notifications/Payments/Messaging modules in **15b–19**. New work follows [architecture.md §4.4](./architecture.md#44-new-feature-checklist).

**Explicit non-goal:** microservices, separate databases per module, or Kubernetes before the monolith modules are proven and tested.

### 7.3 Money movement as first-class domain

Even before Paystack integration, introduce:

```
Payments (domain)
  ├── IPaymentProvider (Paystack, Stripe, …)
  ├── PaymentIntent / Invoice / Receipt entities
  ├── Webhook ingestion
  └── No balance/wallet tables
```

Keeps payment logic out of Booking and avoids rewrite when fintech layer lands.

### 7.4 Nigeria-specific engineering notes

| Reality | Implication |
|---------|-------------|
| WhatsApp is the UI | Payment links + deep links > building chat first |
| Unreliable connectivity | Offline-friendly mobile (React Query cache ✅); SMS fallback for reminders |
| Paystack dominance | First payment provider for NG; abstract behind port |
| CAC verification | Manual admin v1 → API integration v2 (Sprint 19) |

### 7.5 What's built vs. strategic gaps

| Capability | Built (Sprint ≤13) | Gap |
|------------|-------------------|-----|
| Business onboarding + verification | ✅ | Multi-tier badges, CAC API |
| Discovery + geo search | ✅ | Featured/sponsored (later) |
| Booking (request → accept) | ✅ | Notifications, auto-confirm, deposits |
| Reviews | ✅ | Business response to reviews |
| Business portal | ✅ | Subscription gating, deeper analytics |
| Customer my-bookings | ✅ | — |
| Mobile app (Expo) | ✅ | Polish parity with web portal |
| Ask Adeni search | ✅ Rule-based | LLM agent |
| Messaging | ❌ | Sprint 18 |
| Payments | ❌ | Sprint 15 + 17 |
| SaaS billing | ❌ | Sprint 16 |
| Staff management | ❌ | Backlog |
| Quote requests | ❌ | Sprint 19 |

---

## 8. What we explicitly will NOT build (early)

From research — scope traps that kill marketplaces:

- Full CRM / ERP / accounting suite
- Inventory / POS / payroll (until Phase 4+)
- Pay-to-verify badges
- Competing with Google/Yelp on directory size
- Holding customer funds / operating a wallet
- Launching all service categories at once
- **Microservices / multi-repo backend** before modular monolith boundaries are enforced (see [architecture.md](./architecture.md))

---

## 9. Open decisions

| Decision | Options | Recommendation |
|----------|---------|----------------|
| First paid tier price (NG) | ₦5k / ₦10k / ₦15k Pro | Validate with 10 pilot businesses |
| Take rate | 0% launch → 1–2% once payments live | 0% during Lagos pilot |
| Payment provider | Paystack first | Paystack — best NG UX |
| Quote workflow vs fixed booking | Both via business type + service `pricingType` | Category defaults type; v1 = appointment + quote — Sprint 15 + 19 |
| Business type override at onboarding | Category default only vs manual pick | Default from category; show type label during onboarding |
| Confluence sync | Manual / MCP upload | Upload after doc review |

---

## 10. Related documents

| Doc | Purpose |
|-----|---------|
| [architecture.md](./architecture.md) | **Modular monolith** — module map, rules, extraction criteria |
| [sprints.md](./sprints.md) | Execution plan — Sprints 15–20 |
| [prd-v1.1-body.md](./prd-v1.1-body.md) | Functional requirements |
| [markets.md](./markets.md) | Multi-market configuration |
| [frontend.md](./frontend.md) | Web + mobile architecture |
| [observability.md](./observability.md) | Metrics & monitoring |

---

## Appendix — ChatGPT research themes captured

Key themes from the [shared conversation](https://chatgpt.com/share/6a8779af-99d8-83ea-a72d-7de7e8873a1d) incorporated above:

- “Operating system for local service businesses” framing
- Brand naming → **Adeni** (Ade + ni; Yoruba warmth, global neutrality)
- Trust as primary moat (Airbnb analogy)
- AI concierge + business agent
- Subscription + transaction + fintech hybrid revenue
- Non-custodial fintech / payment orchestration
- Modular monolith → microservices when justified
- Lagos + beauty wedge; 100–300 hand-onboarded businesses
- Instant quotes as signature differentiator
- **Business types & workflow capabilities** — one platform, multiple operating models; category-aware defaults (§3.5)
- **Modular monolith architecture** — one API, module boundaries, design for extraction ([architecture.md](./architecture.md))
- Service capability framework (not all businesses = appointments)
- 20-year evolution path to “local service commerce infrastructure”
