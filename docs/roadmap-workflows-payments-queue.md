# Roadmap — workflows, payments, queue & POS bridge

> **Status:** Living product decisions | **Last updated:** October 2026  
> **Parent:** [product-strategy.md](./product-strategy.md) · [roadmap-modules.md](./roadmap-modules.md) · [beauty-wellness-taxonomy.md](./beauty-wellness-taxonomy.md) · [payments.md](./payments.md)  
> **Principle:** Compete without complexity — one primary operating model, market-aware money rails, integrate POS rather than build one.

---

## 1. North star

Adeni wins beauty & wellness by being the **simple floor OS**: discover → book → check in → serve → pay → review — with WhatsApp-native habits and dual-city markets (Lagos + Ottawa).

**Do not** compete with Zenoti on ERP depth (inventory, payroll, memberships, multi-module POS).  
**Do** close the same customer journey with fewer surfaces.

---

## 2. Categories vs workflows

### Decision

Keep **one primary business type** for the Beauty & Wellness wedge: `scheduled_appointment`.  
Add capabilities (queue, deposits, multi-guest) as flags — **not** a new workflow per category.

### Fit matrix (current catalog)

| Fit | Categories | Workflow today | Gap |
|-----|------------|----------------|-----|
| **Fits now** | Massage & Bodywork, Spa & Relaxation, Skincare & Aesthetics, Hair & Grooming, Nails | `scheduled_appointment` + calendar + fixed pricing + multi-service | Queue for walk-in-heavy nails/barbers |
| **Fits, thin** | Fitness, Yoga, Pilates | Same appointment model for 1:1 / private sessions | Class/capacity UX deferred — delivery type may exist on services; do not ship Zenoti-style class modules yet |
| **Catalog only (🟡)** | Recovery, Holistic, Nutrition, Sauna, Experiences | Do not promote in onboarding until appointment UX is rock-solid | — |
| **Different workflow** | Plumbers, electricians (`quote_request`) | Outside wellness Explore wedge | Keep for later / demos; hidden from wellness onboarding |
| **Missing workflow** | Walk-in heavy nails / barbers / some spas | Still appointment-only | `walk_in_queue` capability (below) |

### Rules

1. Discovery **category** ≠ operating model. Categories are browse chips; workflows are capabilities.
2. Fitness / yoga stay **appointment-first** (“book a session”) until class demand is proven.
3. Quote-request trades stay out of the beauty wedge marketing surface.
4. Regulated clinical categories (physio, mental health, medical aesthetics) stay deferred — compliance track, not a workflow shortcut.

### Config source of truth

| Asset | Role |
|-------|------|
| `packages/shared/src/data/category-workflows.json` | Default business type + capabilities per category |
| `packages/shared/src/data/wellness-categories.json` | Enabled / featured Explore chips |
| [beauty-wellness-taxonomy.md](./beauty-wellness-taxonomy.md) | Human-readable taxonomy + delivery types |

---

## 3. Payments — dual market, one port

### Decision

Adeni remains **non-custodial**. Licensed providers move money; Adeni owns intent state, UX, and platform fee math.

| Market | Default provider | Why |
|--------|------------------|-----|
| Lagos / Africa | **Paystack** | Local rails, cards, transfers, subscriptions |
| Ottawa / North America | **Stripe** | SaaS Billing, Connect, Terminal (in-person), CAD/USD, mature webhooks |
| Optional later | Square | When a salon already lives on Square POS |

### Architecture (already aligned)

- Application port: `IPaymentProvider` / `IPaymentOrchestrator` ([payments.md](./payments.md))
- Market config selects default provider (Lagos → Paystack, Ottawa → Stripe)
- Same product surfaces: deposit, pay link, refund, ledger, subscription checkout
- **Never** wallet / balance tables

### Near-term payment work

1. Wire **Stripe** provider beside Paystack (same initialize / webhook / refund contract)
2. Real **subscription billing** checkout (Paystack Subscriptions + Stripe Billing) with signed webhooks
3. Market-aware platform fee from `markets.json`

### Explicit non-goals

- Holding customer or business funds
- Building a card acquirer
- Payroll / tip disbursement as a bank product (reporting can come later)

---

## 4. Close the payment loop (without building POS)

### Decision

Adeni is system of record for **bookings + customer journey**.  
POS (if any) owns **cash drawer / retail SKUs**. We **bridge**, we do not replace the register.

### Three layers

```
1. Online loop (now → harden)
   book → deposit / pay link → webhook → booking confirmed → ledger

2. In-salon loop (next)
   check-in → service complete → collect remaining balance
   (Stripe Terminal / Paystack in-person / “Paid at till”)

3. POS bridge (later)
   sync paid status / ticket total with Square, Lightspeed, Clover, …
   Adeni booking id ↔ external ticket reference
```

### Booking money states (minimum professional loop)

```
confirmed → checked_in → completed → paid
                              ↘ paid_externally (till / POS)
```

| Action | Meaning |
|--------|---------|
| Collect balance | Remaining amount after deposit via provider or Terminal |
| Paid at till | Mark paid; optional external reference |
| POS sync | Webhook/API from partner POS updates `paid` + reference |
| Receipt / ledger | Always visible in Adeni portal |

### POS bridge principles

1. **Optional** — salons without POS still close the loop via Adeni pay link / Terminal / paid-at-till
2. **One-way or thin two-way** — prefer “POS reports paid → Adeni marks paid”; avoid dual inventory masters
3. **Provider-agnostic adapter** — `IPosBridge` (or payments extension) with Square first if demand appears
4. **No retail SKU catalog in Adeni v1** — products stay in POS

---

## 5. Queue + check-in kiosk

### Decision

Ship `walk_in_queue` as a **capability** on the same floor board as appointments — not a separate product island.

```
Booked appointment ──┐
                     ├──→ Floor board ──→ In service ──→ Done / Paid
Walk-in queue     ───┘
```

### Keep it simple

| State | Meaning |
|-------|---------|
| Waiting | In queue / checked in |
| Called | Staff notified client |
| In service | Chair/room occupied |
| Done | Ready for payment / leave |
| No-show | Released capacity |

- Position + rough wait from avg service duration × people ahead  
- Notifications: in-app + WhatsApp/push “You’re up”  
- Capacity shared with calendar staff (same practitioners / chairs)

### Three surfaces, one module

| Surface | Who | Job |
|---------|-----|-----|
| Customer phone | Client | Join waitlist / “I’m here” via link |
| Staff / owner phone or portal | Floor | Next / no-show / assign |
| **Tablet kiosk mode** (mobile app) | Front desk | Fullscreen check-in / join queue — locked, minimal chrome |

**Kiosk = presentation mode** of check-in, not a separate codebase. Build queue + check-in once; tablet runs that screen fullscreen (Flutter business mobile / dedicated kiosk route).

### Out of scope for v1 queue

- Parallel Zenoti queue optimizer / room inventory engine  
- Separate kiosk SKU or hardware partnership  
- Anonymous walk-ins without a phone number (harder no-show recovery)

---

## 6. Sequencing

| Horizon | Focus | Depends on |
|---------|-------|------------|
| **Now** | Harden appointment floor (calendar, staff scope), verification polish, owner “today” dashboard | Shipped staff/calendar |
| **Next** | Stripe provider + live SaaS checkout; booking money states (`checked_in` → `paid`); pay-link GMV | Payments port |
| **Then** | Queue capability + customer check-in + staff “next”; tablet kiosk mode | Floor board + notifications |
| **Later** | POS bridge (Square first); Terminal collect-balance; tips/commission *reporting* | Payment loop trusted |
| **Much later** | Class/capacity UX, payroll, full memberships | Proven demand |

### Suggested sprint-shaped slices (not committed numbers)

1. **Payments NA** — Stripe `IPaymentProvider` + market default  
2. **Payment loop** — check-in / complete / paid (+ paid-at-till)  
3. **Queue v1** — join, board, call next, no-show, ETA  
4. **Kiosk mode** — mobile fullscreen check-in shell  
5. **POS bridge v1** — Square paid-status sync + external reference  

Each slice gets its own `docs/specs/sprint-XX-*.md` before implementation.

---

## 7. What we will not build early

- Full POS / inventory / retail catalog  
- Payroll, tip disbursement, or custody wallets  
- Per-category custom OS modules  
- Class management suites before 1:1 appointment + queue are excellent  
- Pay-to-verify badges  

---

## 8. Related documents

| Doc | Purpose |
|-----|---------|
| [roadmap-modules.md](./roadmap-modules.md) | Full module index + day-in-life + ownership board |
| [product-strategy.md](./product-strategy.md) | Vision, revenue layers, business types |
| [beauty-wellness-taxonomy.md](./beauty-wellness-taxonomy.md) | Categories + delivery types |
| [payments.md](./payments.md) | Paystack orchestration today |
| [subscription-billing.md](./subscription-billing.md) | SaaS billing stub → live providers |
| [specs/sprint-calendar-floor.md](./specs/sprint-calendar-floor.md) | Team vs practitioner calendar |
| [sprints.md](./sprints.md) | Execution plan |
