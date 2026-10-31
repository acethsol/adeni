# Spec: Customer verification & trust foundation

> Platform **customer** trust (email/phone, action gates)—**not** business verification (tenant KYC).  
> **Product direction locked** 2026-10-31 (see §5, §10). **Draft** until Auth0 staging go/no-go (§2).

| Field | Value |
|-------|-------|
| **Sprint** | Unscheduled (after Auth0 staging go/no-go) |
| **Status** | Implemented (Auth0 phone staging go/no-go still required for production phone gates) |
| **Created** | 2026-10-31 |
| **Updated** | 2026-10-31 |
| **Branch (when approved)** | `feature/customer-verification-trust-foundation` from latest `dev` |

See [AGENTS.md](../../AGENTS.md). Full design audit in product review history.

---

## 1. Goal (one sentence)

Establish progressive **account trust** (email and phone verification, explainable state, server-side gates) so Adeni can reduce abuse without mandatory government ID, public discovery login walls, opaque trust scores, or V1 customer moderation.

---

## 2. Context

### Auth0 owns proof; Adeni owns product state

- **Auth0** performs authentication and email/phone verification workflows.
- **Adeni** persists the **minimal verification state** needed for gates, support, and bounded business indicators—not a second identity system.

### Canonical phone (authenticated customers)

- **`Customer.Phone`** is the **only** authoritative phone number on the platform customer record.
- **No** parallel verified-phone column. Validity of “phone verified” for a logged-in customer is:
  - `PhoneVerifiedAt` is set, **and**
  - current normalized E.164 `Customer.Phone` matches **`PhoneVerifiedFingerprint`** (see §3).

### PhoneVerifiedFingerprint (decided)

- **Prefer `PhoneVerifiedFingerprint`** over any second phone field.
- Compute on the server when verification succeeds: **HMAC-SHA256** over normalized E.164 using an **application secret** (Key Vault in staging/prod)—**not** a plain unsalted hash.
- Store fingerprint only; never store OTP codes or verification secrets in SQL.
- If `Customer.Phone` changes (Auth0 sync or profile update), re-normalize and compare to fingerprint → **`phoneVerified` is false** until the new number completes verification and a new fingerprint is written.

### Public walk-in queue (decided — not the same persistence path)

- **Phone-verified guest/anonymous participation.** **No** full Adeni account required to join a public queue.
- **Product flow:** **name + phone → phone verification → join queue.**
- **Do not implement custom Adeni OTP in V1.** The technical step (Auth0 passwordless, Auth0 phone connection, Twilio Verify orchestrated by Auth0, etc.) is **explicitly dependent** on the Auth0/Twilio verification design validated in staging (§2 gate).
- Queue join API must accept **server-trusted proof of phone verification** (e.g. short-lived token/session from the chosen provider)—not client-asserted `phoneVerified: true`.

### Auth0 phone verification — staging go/no-go (blocker)

**Before implementing any phone-dependent production gate** (waitlist, retention checkout, public queue, authenticated `phoneVerified`):

| Requirement | Detail |
|-------------|--------|
| Markets | End-to-end test with a **Canadian** and a **Nigerian** number |
| Delivery | SMS (or configured channel) received |
| Verification | User completes provider verification flow |
| Claims | Document exactly what the **API access token** (and ID token if needed) exposes: `phone_number`, `phone_number_verified`, and any **custom claims** required via Login Action |
| Decision | **Go / no-go** recorded in `docs/auth0-setup.md` (or `docs/customer-verification.md`) |

**Do not ship production assumptions** about phone verification until this gate passes in **staging**.

### Separation from other domains

- Business verification (tenant KYC), retention ledgers, and [business post-visit customer feedback](./business-post-visit-customer-feedback.md) (private CRM notes) are **out of scope** and must not feed platform trust.

### Modules

Identity (`Customer`), Api, Booking, Messaging, Retention, Queue (public join).

---

## 3. Minimal V1 trust persistence (decided)

**Re-evaluated:** a separate trust table is **not required** for V1 unless implementation review shows a cleaner 1:1 split for erasure-only concerns.

**Preferred columns** (all on `identity.customers` **or** a 1:1 `identity.customer_verification` with **only** these fields—pick one during implementation, not both):

| Column | Purpose |
|--------|---------|
| `EmailVerifiedAt` | First time server observed verified email for this customer (NULL = never) |
| `PhoneVerifiedAt` | When current canonical phone was last verified (NULL = never / invalid) |
| `PhoneVerifiedFingerprint` | HMAC-SHA256 of normalized E.164 at verify time (NULL = no binding) |
| `UpdatedAt` | Last trust-state write |

**Not in V1 DACPAC:** `IdentityVerificationStatus`, KYC fields, trust score, **`TrustStatus`**, **`TrustRestrictedAt`**, restriction reason codes, or other moderation schema—**admin restrict/suspend is deferred** (§4).

Normalize **`Customer.Phone`** to E.164 on trusted sync paths (Canada, Nigeria).

---

## 4. In scope (V1)

- [ ] DACPAC minimal trust fields (§3) on `Customer` or 1:1 record
- [ ] `ICustomerTrustQuery` + `ICustomerVerificationPolicy` (explicit checks, not a rules engine)
- [ ] `ICustomerTrustSyncService` — idempotent updates from **trusted JWT claims** only
- [ ] **`GET /api/v1/me/verification`** for authenticated customers
- [ ] Trust sync on login / customer bootstrap / token refresh; optional **`POST /api/v1/me/verification/refresh`** (narrow, rate-limited, no body flags) — see §6
- [ ] Customer **`/app/profile`** — Account verification (email / phone); no KYC UI
- [ ] **Server-side gates** per §5 matrix
- [ ] **Public queue** guest flow: name + phone + provider-verified phone proof → join (no account)
- [ ] **Business F1** `trust: { phoneVerified, customerSince }` only
- [ ] **Admin read-only:** verification timestamps + account age + existing export fields — **no** restrict/restore actions, **no** restriction status in V1
- [ ] Privacy/erasure: clear verification timestamps + fingerprint
- [ ] Auth0 email verification on customer connection; **staging go/no-go** for phone (§2)

## 5. Out of scope (V1)

- Government ID / KYC / biometrics
- Trust score / ML fraud / cross-business blocklist
- **Custom Adeni OTP** (Twilio or otherwise) without approved Auth0/Twilio design spec
- **Admin customer restrict/suspend/restore** and any speculative moderation schema
- Mandatory phone for **standard booking** (email only)
- **Default messaging** email/phone verification gates (§5 matrix)
- Messaging step-up unless a **future** abuse program approves (not V1)
- Using business visit notes as trust input

---

## 6. V1 action matrix (product direction)

| Action | Account / auth | Email verified | Phone verified | Notes |
|--------|----------------|----------------|----------------|-------|
| **Browse / discover** | None (anonymous) | — | — | Unchanged |
| **Standard booking** | Customer JWT | **Yes** | No | Deposit/payment for booking same as booking |
| **Messaging** | Customer JWT | No | No | **Existing** rules: valid conversation ownership/context only |
| **Review** | Customer JWT | **Yes** | No | Plus **existing** completed + owned booking + one per booking |
| **Waitlist** | Customer JWT | **Yes** | **Yes** | After §2 go/no-go |
| **Package / membership purchase** | Customer JWT | **Yes** | **Yes** | Retention/commerce; after §2 go/no-go |
| **Public walk-in queue** | **No full Adeni account** | — | **Yes** | Name + phone → verify → join; proof via Auth0/Twilio design, not custom OTP |

**Gift card purchase:** treat same as package/membership (email + phone) unless product narrows later—money-adjacent flow.

**Messaging (explicit):** Do **not** add phone or email verification as a new default gate in V1. Contextual step-up remains a **later** option if abuse data justifies it.

---

## 7. API contract

### Authenticated customer

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| `GET` | `/api/v1/me/verification` | Customer JWT | `emailVerified`, `phoneVerified` (derived: timestamps + fingerprint match), `emailVerifiedAt`, `phoneVerifiedAt`, optional UX hints |

**Trust synchronization**

| Mechanism | Description |
|-----------|-------------|
| **Primary** | `ICustomerTrustSyncService.ApplyFromPrincipal` on customer bootstrap / pipeline — idempotent; sets `EmailVerifiedAt` / `PhoneVerifiedAt` + fingerprint from JWT when claims indicate verified and phone matches canonical E.164 |
| **Secondary** | `POST /api/v1/me/verification/refresh` — rate-limited; JWT only (optional userinfo if spec’d post–staging gate); no client verification booleans |

**Do not implement:** `POST /api/v1/me/trust/sync` with client-supplied verified flags.

### Public queue (guest)

Design detail **blocked on §2** but product shape is fixed:

1. Client collects display name + phone (E.164 normalized server-side).
2. Client completes **provider** phone verification (Auth0/Twilio—mechanism TBD by staging outcome).
3. Client calls existing or extended **`POST /api/v1/public/queue/{slug}/join`** with name, phone, and **provider-issued verification proof** (token/session id validated server-side—not a boolean).

Rate limits on join unchanged or tightened as needed.

### Business (F1)

```json
{
  "trust": {
    "phoneVerified": true,
    "customerSince": "2026-01-15T00:00:00Z"
  }
}
```

No email verified, restriction, or risk fields.

**Errors:** `customer.email_not_verified`, `customer.phone_not_verified`, `customer.verification_required`, `queue.phone_not_verified` (stable codes + i18n).

---

## 8. Cross-cutting impact

| Area | Impact |
|------|--------|
| Tenant isolation | Trust query scoped to authenticated `CustomerId`; queue proof scoped to join attempt |
| PII | Canonical phone only; mask in logs; fingerprint is non-reversible without secret |
| Secrets | HMAC key in Key Vault; never log OTP, tokens, or raw verification payloads |
| Rate limiting | `verification/refresh`; public queue join |
| Claim freshness | Sync on login/refresh after Auth0 verify UI |
| Messaging | **No** new verification middleware on message POSTs in V1 |

---

## 9. Tests

- Matrix gates: booking (email), waitlist/retention (email+phone), review (email + booking rules)
- Fingerprint: match → verified; phone change → unverified until re-verify; HMAC uses configured secret
- Public queue: join rejected without valid provider proof; accepted with proof; **no** customer account required
- Spoof: client cannot set verified via body
- Cross-customer trust not readable
- Business `trust` only `phoneVerified` + `customerSince`
- Messaging regression without email/phone verified
- Erasure clears trust fields
- Discovery anonymous; no trust in SSR TransferState
- CA/NG E.164 normalization

Run: `dotnet test Adeni.slnx -c Release`

---

## 10. Acceptance criteria

- [ ] §2 staging go/no-go **passed** before phone gates enabled in production config
- [ ] Minimal trust columns only (§3); **no** restriction/moderation schema in V1
- [ ] `phoneVerified` for accounts requires fingerprint match on canonical `Customer.Phone`
- [ ] Public queue works for **guests** with verified phone only (no account)
- [ ] No custom Adeni OTP in V1
- [ ] Messaging unchanged regarding verification gates
- [ ] Backend enforcement on all gated actions

---

## 11. Decisions log (formerly open questions)

| Topic | Decision |
|-------|----------|
| Public queue account requirement | **No full account** — phone-verified guest |
| Public queue flow | **Name + phone → verify → join** |
| Public queue OTP | **No custom Adeni OTP** — Auth0/Twilio design from staging |
| Auth0 phone NG/CA | **Staging go/no-go required** before phone gates (§2) |
| Phone binding | **`PhoneVerifiedFingerprint`** (HMAC-SHA256 + app secret); canonical **`Customer.Phone`** |
| Trust table vs columns | **Minimal fields** on `Customer` or 1:1 record—implementer choice, not both |
| Admin restrict/suspend | **Deferred** — no V1 schema or Admin mutations |
| Messaging verification gates | **None in V1** — auth + conversation context only |
| Identity / KYC status column | **Not in V1** |

---

## 12. Remaining blockers (true)

| Blocker | Owner | Unblocks |
|---------|--------|----------|
| **Auth0/Twilio staging validation** (CA + NG SMS, verify flow, API token claims) | Eng + Auth0 config | Waitlist phone gate, retention phone gate, authenticated `phoneVerified`, **public queue proof validation** |
| **Queue join proof contract** (exact token/session shape after staging) | Eng spec addendum post–go/no-go | Public queue implementation detail |

Everything else in this spec is **direction-complete** for implementation planning.

---

## 13. Implementation sequence (after §2 go/no-go)

1. Document staging outcomes in `docs/auth0-setup.md` / `docs/customer-verification.md`  
2. DACPAC + domain (§3) + HMAC fingerprint service  
3. `ICustomerTrustSyncService` + `GET /me/verification` (+ optional refresh)  
4. Profile UI + token refresh after Auth0 verify  
5. Gates: booking (email), waitlist + retention (email+phone), review (email)  
6. Public queue guest verification integration (provider proof)  
7. Business F1 `trust` subset  
8. Admin read-only export fields + erasure  
9. Tests + docs  

**Not in V1 sequence:** Admin restrict/suspend, messaging verification gates, custom OTP, KYC.
