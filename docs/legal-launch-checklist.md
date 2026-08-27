# Legal launch checklist (pre-production)

> **Status:** Living checklist | **Not legal advice** — engage Nigeria-qualified counsel before launch  
> **Trigger:** [@shrug.manny reel](https://www.instagram.com/reel/DbO4zvJR1k8/) — limitation of liability, governing law, data deletion, indemnification  
> **Engineering parent:** [prd-v1.1-body.md §10.3](./prd-v1.1-body.md) (SOC2-09), [product-strategy.md](./product-strategy.md)

Adeni is a **marketplace** (customers + businesses + payments orchestration). Legal readiness is separate from the Murphy **technical** 13 layers but equally blocking for real users in Lagos.

---

## 1. Documents to publish (lawyer-drafted)

| Document | Must include (reel + marketplace) | Owner | Status |
|----------|-------------------------------------|-------|--------|
| **Privacy Policy** | What we collect (Auth0, profile, bookings, Paystack metadata), lawful basis, retention, NDPR rights, contact DPO/privacy email | Legal + product | 🔲 Draft — placeholder at [/privacy](https://adeni.io/privacy) |
| **Terms of Service** | Acceptable use, account termination, disclaimers, **limitation of liability**, **governing law** (Nigeria), dispute resolution | Legal | 🔲 Draft — placeholder at [/terms](https://adeni.io/terms) |
| **Business / Partner Terms** | Verification, listing accuracy, booking obligations, fees, chargebacks (Paystack), **indemnification** for business content | Legal | 🔲 Not started |
| **Cookie notice** | If App Insights browser RUM or marketing cookies (Sprint 20) | Legal + eng | 🔲 Not started |

**Non-custodial payments:** Terms must state Adeni orchestrates via licensed providers (Paystack); Adeni does not hold customer or business funds — see [product-strategy.md §4.3](./product-strategy.md#43-fintech--orchestration-not-custody).

---

## 2. Engineering already built (implement what policies promise)

| Capability | Implementation | Doc |
|------------|----------------|-----|
| Customer data export | `GET /admin/customers/{id}/export` | [prd-v1.1-body.md §10.3](./prd-v1.1-body.md) |
| Customer data deletion | `POST /admin/customers/{id}/delete` (30-day purge) | Same |
| Admin privacy UI | `AdminCustomerPrivacyPanel` | Web admin |
| PII masking in logs | `PiiMasker`, Serilog policy | [AGENTS.md](../AGENTS.md) |
| Audit trail | Admin mutations → `admin.audit_logs` | SOC2-01 |
| Paystack orchestration | No wallet tables | [docs/payments.md](./payments.md) |

Privacy Policy must describe **how** a customer exercises export/delete (today: admin-assisted v1; self-serve v2 TBD).

---

## 3. App & web wiring

| Item | Requirement | Status |
|------|-------------|--------|
| Footer Privacy link | `/privacy` | ✅ Wired (this sprint) |
| Footer Terms link | `/terms` | ✅ Wired (this sprint) |
| Sign-up / register acceptance | Checkbox + link to Terms & Privacy before account creation | 🔲 Todo |
| Business registration | Accept Business Terms + Privacy | 🔲 Todo |
| Booking checkout | Link to Terms (and refund/cancellation policy when payments live) | 🔲 Todo |
| Mobile app | In-app links to same URLs | 🔲 Todo |
| Confluence / legal review | [Privacy Policy Legal Review Checklist](https://aceth.atlassian.net/wiki/spaces/SD/pages/26738699) | 🔲 Legal sign-off |

---

## 4. Reel clause → Adeni mapping

| @shrug.manny clause | Where it lives | Adeni action |
|---------------------|----------------|--------------|
| **Limitation of liability** | Terms of Service | Lawyer caps damages; exclude indirect/consequential where permitted |
| **Governing law** | Terms of Service | Specify Nigeria (and Lagos jurisdiction if advised) |
| **Data deletion policy** | Privacy Policy + process | Document admin export/delete APIs; publish retention periods |
| **Indemnification** | Terms + Business Terms | Businesses indemnify for false listings, IP, regulatory breaches |

---

## 5. Nigeria / NDPR specifics (for counsel)

- NDPR lawful processing and data subject rights (access, rectification, erasure)
- Cross-border transfers if Auth0/Azure regions outside Nigeria
- CAC / business verification copy aligned with verification tiers (Sprint 19)
- Consumer protection for booking deposits via Paystack
- NDPC registration if required at scale

---

## 6. Launch gate (legal)

Do **not** open to general Lagos public until:

- [ ] Privacy Policy and Terms published (lawyer-approved), not placeholder copy
- [ ] Business Terms published if businesses can register publicly
- [ ] Footer and registration flows link to live policies
- [ ] Privacy Policy accurately describes export/delete process
- [ ] Paystack / Auth0 sub-processor disclosure in Privacy Policy
- [ ] Internal runbook: who handles privacy requests (email / admin queue)

Optional before scale: Rapid legal review of marketplace clauses; align with [SOC 2 Compliance Framework](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170).

---

## 7. Related docs

| Doc | Purpose |
|-----|---------|
| [AGENTS.md](../AGENTS.md) | Technical production readiness |
| [docs/specs/](./specs/) | Feature specs before code |
| [docs/payments.md](./payments.md) | Paystack, non-custodial posture |
| [docs/sprints.md](./sprints.md) | Sprint 20 deploy & observability |
