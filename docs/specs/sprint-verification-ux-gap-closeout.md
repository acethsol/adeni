# Spec: Verification UX gap closeout

> Authoritative plan to close first-time business verification + trust-surface gaps between API, portal, admin, and Discover. Implementation follows phased delivery below; **do not** treat this as a new product sprint number — backlog item on `dev` until scheduled.

| Field | Value |
|-------|-------|
| **Sprint / track** | Backlog — Verification UX gap closeout (post Sprint 19 / 38c) |
| **Author** | Oladayo Jolaoso |
| **Status** | Shipped on `dev` (Phases 1–3) |
| **Created** | 2026-10-26 |

---

## 1. Goal (one sentence)

Ship a coherent **first-time verification** experience (submit → admin review → approve/reject → Discover visibility) and align **trust signals** across portal, admin, and Discover without introducing pay-to-verify or document file upload in early phases.

---

## 2. Context

- **Onboarding flow:** Register → complete profile/location → submit verification references → admin approves → tenant `Verified` → discovery eligibility ([Sprint 1](../sprints.md), [Sprint 6](../sprints.md)).
- **Trust depth (Sprint 19):** Tiered **post-verification** badges (phone, CAC, address, license) via `/api/v1/tenant/verification/badges` and admin `/api/v1/admin/verification/*` — **separate** from the first-time pending queue ([sprint-19-trust-quotes.md](./sprint-19-trust-quotes.md)).
- **Notifications (Sprint 38):** Resend/Twilio module exists; verification lifecycle emails are **not** wired today.
- **Gap source:** Cloud matrix review + portal/admin/discover parity audit (Oct 2026).

**Modules:** Tenancy (`IBusinessOnboardingService`, `IAdminBusinessService`), Trust (`IVerificationBadgeService`), Discovery (public profile/card trust fields), Notifications (optional Phase 2).

**Strategy alignment:** [product-direction.md](../product-direction.md) (Ottawa + Lagos, beauty & wellness V1); [product-strategy.md](../product-strategy.md) §3 (trust, not pay-to-verify); [roadmap-modules.md](../roadmap-modules.md) verification module.

---

## 3. In scope

### API

- [x] Expose **admin-review detail** on tenant profile: submitted document **types**, **reference numbers** (masked where appropriate for support — full refs for tenant owner on their profile), and **submittedAt** per document (already stored; not fully exposed on `GET /api/v1/tenant/profile`).
- [x] Expose **last rejection reason** on tenant profile when `status === Rejected` (persisted field — today reason exists only in admin audit JSON).
- [x] Align **reference number minimum length = 5** in `packages/shared` Zod + portal client validation (API already enforces 5 in `SubmitVerificationAsync`).
- [x] Admin pending list: **`submittedAt`** for queue sort/display = latest verification document submit time (fallback `tenant.CreatedAt` if no docs).
- [ ] Optional admin **`GET /api/v1/admin/businesses/pending/{tenantId}`** detail DTO if list payload is insufficient for drawer UX (may reuse list row shape if extended).

### Portal

- [x] **Verification stepper** on profile verification tab (Draft → submitted → in review → verified / rejected), reusing dashboard `verification_status` copy patterns where possible.
- [x] **Suspended** status callout (status `4`) on verification tab — not only business-details lock hint.
- [x] **Submitted documents list** (type label + submitted date; reference optional/masked tail for owner).
- [x] **Rejection banner** with admin reason + CTA to resubmit when `Rejected`.
- [x] **Prerequisite copy** before submit (active location, profile fields complete) matching API validation messages (i18n keys in `@adeni/shared`).
- [x] **Validation parity:** client min 5 chars for reference (fix current 4-char check).
- [x] **Lock public-page / branding edits** while `PendingVerification` (API today locks core profile only; `UpdatePublicPageAsync` still allows edits — align with “editing paused during review” copy).

### Admin

- [x] **Detail panel or expandable row** showing document types + reference numbers for reviewers (data already in list API `documents[]`; UI shows count pill only).
- [x] **Confirm approve** dialog (destructive-light guard — name + market confirm).
- [x] **Reject modal** with validated reason (min 10 chars) — replace `prompt()`.
- [ ] **Empty state** — present; verify i18n + export still works (no change unless QA finds gap).
- [ ] **Toasts** — success paths exist; standardize error toasts on approve/reject failure (minor).
- [x] **Real submittedAt** column — use document submit time, not tenant `createdAt`.

### Discover

- [ ] **Badge consistency audit:** ensure “Verified” / trust chip uses same rule on **all** card and profile surfaces (`verifiedSince` today; Sprint 19 **`verificationBadges`** stack not rendered on Discover — add where product expects tier badges vs base verified).
- [ ] Document explicit rule in spec acceptance: **base verified** = `verifiedSince` non-null; **supplemental badges** = `verificationBadges[]` when present.

### Cross-cutting

- [x] Integration tests for new profile fields and reject-reason persistence.
- [ ] Admin approve/reject audit unchanged (`business.approved`, `business.rejected`); submit audit **`business.verification_submitted`** — **already shipped**.

---

## 4. Out of scope

- **Verification document file upload** (PDF/image) — Phase 3; V1 remains reference-number + admin manual check ([product-strategy.md](../product-strategy.md) CAC manual v1).
- **Auth0** rule changes, new roles, or MFA policy changes (admin MFA remains as today).
- **Automated CAC / third-party identity API** (Sprint 19g research only).
- **Pay-to-verify** or gating basic verification behind payment (see open questions).
- **Microservices** or new modules beyond Tenancy/Trust/Notifications touch points.
- **Category-specific license gates** (Sprint 19h) — only adjust copy if verification UX references them; no new category rules in this track.

---

## 5. User stories

| Actor | Story |
|-------|--------|
| Business owner | I see where I am in verification, what I submitted, and why I was rejected so I can fix and resubmit without contacting support. |
| Business owner | While admin review is pending, I cannot change public-facing page design in ways that confuse reviewers (consistent with profile lock). |
| Admin reviewer | I see document types and reference numbers, confirm before approve, and reject with a proper reason that the business will see. |
| Admin reviewer | Queue “Submitted” date reflects when they applied for verification, not account creation. |
| Consumer | I see consistent trust signals on discovery cards and business profiles. |
| Ops | Rejection reasons are queryable on the tenant record (not audit-only) for support. |

---

## 6. API contract

All routes under `/api/v1/`. Mutations require Auth0 JWT; tenant routes require `X-Tenant-Id` + claim match; admin routes require admin MFA policy when enabled.

### 6.1 Existing (no change expected except extensions)

| Method | Route | Auth | Notes |
|--------|-------|------|--------|
| POST | `/api/v1/tenant/verification/submit` | Business JWT + tenant | Min 5 char refs; Draft/Rejected only; audit **`business.verification_submitted`** ✅ |
| GET | `/api/v1/tenant/profile` | Business JWT + tenant | **Extend** response |
| GET | `/api/v1/admin/businesses/pending` | Admin MFA | **Extend** row `submittedAt` semantics |
| POST | `/api/v1/admin/businesses/{id}/approve` | Admin MFA | Audit **`business.approved`** ✅ |
| POST | `/api/v1/admin/businesses/{id}/reject` | Admin MFA | Body `{ reason }` min 10 ✅; **persist reason on tenant** |
| GET/POST | `/api/v1/tenant/verification/badges` | Business | Sprint 19 supplemental badges — Phase 2 portal UI |
| GET/POST | `/api/v1/admin/verification/pending-badges` | Admin | Trust badge queue — not first-time onboarding queue |

### 6.2 Profile extensions (`BusinessProfileResponse` / `businessProfileSchema`)

```json
{
  "status": 3,
  "verificationRejectionReason": "CAC number does not match business name on file.",
  "verificationDocuments": [
    {
      "documentType": 0,
      "referenceNumber": "RC123456",
      "submittedAt": "2026-10-20T14:00:00Z"
    }
  ]
}
```

| Field | Type | When present |
|-------|------|----------------|
| `verificationRejectionReason` | `string \| null` | Last reject reason when `status === Rejected`; cleared on successful resubmit or approve |
| `verificationDocuments[].referenceNumber` | `string` | Tenant owner view (support may mask in admin export if policy added later) |
| `verificationDocuments[].submittedAt` | ISO-8601 | Existing DB field |

**Errors:** stable `code` via existing validation/forbidden patterns; new codes only if needed (`verification.locked`, `verification.invalid_reference`).

### 6.3 Pending queue row (`pendingBusinessSchema`)

```json
{
  "id": "uuid",
  "name": "Studio X",
  "slug": "studio-x",
  "marketId": "lagos",
  "status": 1,
  "createdAt": "2026-09-01T10:00:00Z",
  "submittedAt": "2026-10-20T14:00:00Z",
  "documents": [
    {
      "documentType": "cac",
      "referenceNumber": "RC123456",
      "submittedAt": "2026-10-20T14:00:00Z"
    }
  ]
}
```

| Field | Rule |
|-------|------|
| `submittedAt` | `Max(documents.submittedAt)` or null → UI falls back to `createdAt` |
| `documents[].documentType` | Lowercase enum string (already mapped in admin list API) |

### 6.4 Submit request (Zod alignment only)

```json
{
  "documents": [
    { "documentType": 0, "referenceNumber": "RC12345" }
  ]
}
```

`referenceNumber`: **`min(5)`** in `@adeni/shared` `verificationDocumentRequestSchema` (match server).

### 6.5 Optional Phase 2 — notification triggers (no new public routes)

Internal: `INotificationService` or domain events after submit / approve / reject — templates in Sprint 38c catalog; ids TBD in implementation spec addendum.

---

## 7. UI acceptance criteria

### Portal (`apps/portal`)

- Verification tab shows **stepper** states for status 0,1,2,3,4 with accessible labels (i18n).
- Status **1 (Pending):** no submit form; shows submitted docs list + “in review” copy; business tab shows lock hint (existing).
- Status **3 (Rejected):** banner with `verificationRejectionReason`; submit form enabled; stepper shows rejected step.
- Status **4 (Suspended):** prominent callout on verification tab; submit disabled.
- Reference field validates **≥ 5** characters before API call; error string updated in catalog-en/fr.
- Prerequisite checklist visible when Draft/Rejected (location + required profile fields).
- `/public-page` (and related branding saves) **disabled or blocked** when status Pending — match API enforcement.

### Admin (`apps/admin`)

- Pending queue row expand or side panel lists each document: type label, reference, submitted date.
- Approve opens **confirm** modal; Reject opens **modal** with textarea, min 10 chars, no `prompt()`.
- Submitted column uses **`submittedAt`** from API.
- Approve/reject success and failure use `AdeniFeedbackService` consistently.

### Discover (`apps/discover`)

- Inventory all business surfaces (discovery card, hero stage, profile header, SEO JSON-LD if applicable); **verified** chip rule documented and consistent.
- If `verificationBadges` should display on profile: render badge stack per Sprint 19f (Phase 1 or 1b — product decision in checklist).

---

## 8. Phased implementation plan

### Phase 1 — Quick wins (target: single PR or 1–2 small PRs)

**Goal:** Parity and reviewer UX without new infrastructure.

1. Domain: `Tenant.VerificationRejectionReason` (nullable) + EF migration; clear on resubmit/approve.
2. Application/Infrastructure: map rejection reason + document refs in `MapProfileAsync`; persist reject reason in `RejectAsync` (in addition to audit JSON); compute pending `submittedAt`.
3. `packages/shared`: extend schemas; `min(5)` on submit; i18n for prerequisite/rejection/stepper.
4. `packages/api-client`: typed fields only if shapes change.
5. Portal: stepper, banners, docs list, validation 5, suspended callout, public-page lock (UI + API guard on `UpdatePublicPageAsync`).
6. Admin: detail docs in UI, confirm approve, reject modal, submittedAt column.
7. Discover: verified/badge consistency pass (minimal — shared component or rule table in code comments + tests).
8. Tests: `BusinessOnboardingServiceTests`, `AdminBusinessServiceTests`, one API integration test for profile rejection reason.

**Copy-only alternative (if email deferred):** Soften portal “admin will review” copy to set expectations (no SLA promise); document in Phase 2.

### Phase 2 — Notifications & supplemental trust (after Phase 1 merged)

1. Wire **transactional email** (and optional SMS) on verification submitted, approved, rejected — use Notifications module, PII-safe templates, tenant owner email from profile (masked in logs).
2. Portal **trust badge request** UI (`/settings` or profile sub-section) calling existing `/api/v1/tenant/verification/badges` — parity with admin trust-badges queue (Sprint 19b backend done).
3. **Pay-before-verify policy** decision implemented only if leadership chooses gate (default: **no gate** — verification before Discover, billing independent).

### Phase 3 — Document upload (future)

1. Spec addendum: blob upload purpose `verification_document`, virus scan policy, admin download audit, retention.
2. Portal file picker + API multipart or signed URL pattern (mirror gallery/media patterns).
3. Admin viewer for uploaded files; **never** expose raw docs on public APIs.

#### Phase 3 addendum (implemented)

| Topic | Decision |
|-------|----------|
| **Blob purpose** | `verification_document` via `POST /api/v1/tenant/media/upload-url`; keys `tenants/{tenantId}/verification/{uuid}.pdf\|jpg\|png` (private container / signed download). |
| **Submit binding** | Optional `storageFileKey` on verification submit; prefix + `ExistsAsync` validation (same as cover/gallery). |
| **Admin access** | `GET /api/v1/admin/businesses/{tenantId}/verification-documents/{documentId}/download-url` — MFA admin; audit `verification_document.downloaded` (tenant id in metadata only). |
| **Public APIs** | Never expose storage keys or download URLs on profile/discovery; tenant profile shows `hasUploadedFile` boolean only. |
| **Virus scan** | Deferred to Sprint 20 infra — treat uploads as trusted admin-review queue input until scanner wired at blob edge. |
| **Retention** | Follow tenancy lifecycle: purge keys when tenant hard-deleted; soft-delete / legal hold out of scope for V1. |

---

## 9. Cross-cutting impact

| Area | Impact |
|------|--------|
| **Tenant isolation** | Profile and submit remain tenant-scoped; admin pending list is global admin — no cross-tenant IDs in URLs without admin auth. |
| **Auth / roles** | Business JWT for tenant routes; admin approve/reject requires admin MFA policy when `Auth0:RequireMfaForAdmin=true`. |
| **Cache** | Invalidate `tenant:{id}:profile` and location profile keys on reject/approve/submit (existing patterns). |
| **Audit** | Submit ✅ `business.verification_submitted`; approve/reject ✅; **no duplicate** middleware rows ([audit-runbook.md](../audit-runbook.md)). |
| **PII** | Reference numbers and rejection reasons are sensitive — **do not log** full values; audit metadata for reject may remain reason in JSON (admin-only). Public discovery never exposes refs. |
| **Observability** | Optional App Insights events `verification.submitted|approved|rejected` (Sprint 20 — not blocking). |

---

## 10. Tests

- [x] Unit: reject reason persisted and cleared on resubmit; pending `submittedAt` = max document time.
- [x] Unit: `UpdatePublicPageAsync` fails when `PendingVerification`.
- [x] Integration: `GET /api/v1/tenant/profile` returns rejection reason after reject.
- [x] Integration: submit with 4-char reference → 400 validation.
- [x] Integration: admin reject with short reason → 400.
- [ ] Tenant cross-access: unchanged (profile is own-tenant only).

Run: `dotnet test Adeni.slnx -c Release`

---

## 11. Files to touch (implementation checklist)

| Layer | Files (expected) |
|-------|------------------|
| **Domain** | `Adeni.Domain/Tenancy/Tenant.cs` (rejection reason); optional value object |
| **Application** | `IBusinessOnboardingService.cs` (`BusinessProfileResponse`, `VerificationDocumentResponse`); `IAdminBusinessService.cs` / `PendingBusinessResponse` |
| **Infrastructure** | `BusinessOnboardingService.cs`, `AdminBusinessService.cs`; EF configuration; migration |
| **Api** | `TenantController.cs`, `AuthController.cs` (if DTO mapping); OpenAPI annotations |
| **Tests** | `BusinessOnboardingServiceTests.cs`, `AdminBusinessServiceTests.cs`, `PortalSoc2AuditIntegrationTests.cs`, API integration tests |
| **packages/shared** | `schemas.ts`, `catalog-en.ts`, `catalog-fr.ts`, `VERIFICATION_DOCUMENT_LABELS` |
| **packages/api-client** | profile/pending parsers if needed |
| **apps/portal** | `profile.component.*`, `public-page` route guard or component disable |
| **apps/admin** | `pending.component.*`, shared reject modal component (optional in `@adeni/ui`) |
| **apps/discover** | `discovery-business-card.*`, `business.component.*`, `hero-category-stage.*` |

---

## 12. Acceptance criteria (release)

- [x] Cloud matrix checklist (§13) all Phase 1 items checked.
- [x] No pay-to-verify behavior introduced.
- [x] Reference min length 5 on client and server.
- [x] Rejection reason visible to business in portal; stored on tenant.
- [x] Admin reject uses modal; approve uses confirm.
- [x] Pending queue submitted date reflects verification submit time.
- [ ] Docs-only PR for this spec approved before Phase 1 code merge.

---

## 13. Gap matrix (Cloud + review)

Legend: ☑ shipped · ☐ gap · ~ partial

### API

| Item | Status |
|------|--------|
| Submit verification | ☑ |
| Approve / reject | ☑ |
| Audit on approve / reject | ☑ |
| Audit on submit (`business.verification_submitted`) | ☑ |
| Admin detail: doc types + refs in API payload | ~ (list includes docs; no single-tenant detail route — optional) |
| Rejection reason on profile API | ☑ |
| Reference length 5 everywhere (shared + portal) | ☑ |
| File upload | ☑ (Phase 3 — optional file on submit) |

### Portal

| Item | Status |
|------|--------|
| Status + submit form + profile lock (pending) | ☑ |
| Verification stepper | ☑ |
| Suspended callout (verification tab) | ☑ |
| Submitted docs list | ☑ |
| Rejection banner + reason | ☑ |
| Validation parity (5 chars) | ☑ |
| Prerequisite copy | ☑ |
| Lock public-page edits while pending | ☑ |

### Admin

| Item | Status |
|------|--------|
| Queue + approve/reject actions + dashboard link | ☑ |
| Detail panel refs/types | ☑ |
| Confirm approve | ☑ |
| Reject modal (not prompt) | ☑ |
| Empty state | ☑ |
| Toasts | ~ |
| Real submittedAt | ☑ |

### Discover

| Item | Status |
|------|--------|
| Verified / trust on cards & profile via `verifiedSince` | ~ |
| Badge stack / consistency all surfaces | ☑ (profile stack; cards keep base verified) |

### Extra (review)

| Item | Status | Phase |
|------|--------|-------|
| Transactional email on lifecycle | ☑ | 2 |
| Soften copy if no email | ~ (email shipped) | 1 optional |
| Public-page lock while pending | ☑ | 1 |
| Portal trust-badge request UI | ☑ | 2 |
| Pay-before-verify policy | ☑ **No gate** (documented) | 2 |
| product-strategy vs Sprint 19 reconcile | ~ | docs |

---

## 14. Open questions

| Question | Proposed decision |
|----------|-------------------|
| Pay-before-verify (require SaaS payment before admin queue)? | **No for V1** — verification is trust gate for Discover; billing trial starts on approve ([AdminBusinessService](../src/Adeni.Infrastructure/Admin/AdminBusinessService.cs) trial on approve). Revisit only if finance requires. |
| Show full reference numbers to admin vs last-4 mask? | **Full refs in admin UI** for manual CAC check; never on public APIs. |
| Discover: show Sprint 19 badge stack on cards? | **Phase 1b:** profile page yes if badges present; cards keep compact “Verified” unless crowded — UX sign-off in PR. |
| Email vs in-app only for reject? | **Phase 2** email; Phase 1 must surface reason in portal. |
| Single-tenant admin detail endpoint? | **Defer** unless drawer needs more than list row; extend list DTO first. |

---

## 15. Documentation reconcile

- [product-strategy.md](../product-strategy.md) §3 competitive table row for tiered verification should reflect **Sprint 19 shipped** (badges + quote depth) while this spec covers **first-time onboarding UX** gaps — see one-line note in product-strategy (same PR as spec).
