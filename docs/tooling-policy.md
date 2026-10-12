# Adeni tooling policy (V1)

> **Status:** Canonical for agents | **Date:** 2026-10-12  
> Freezes which third-party tools we adopt for ops, product learning, notifications, maps, and media. Do not add overlapping stacks without updating this doc.

## Decision summary

| Job | Tool | When |
|-----|------|------|
| Platform ops (API health, deps, latency SLOs, Azure alerts) | **Azure Application Insights** | Sprint 20 / staging→prod |
| Client error tracking (grouped crashes, releases) | **Sentry** | Sprint 20 — Discover first, then portal/admin/Flutter |
| Product analytics + session replay | **PostHog** | After real users on Discover (and portal when useful) |
| Correlation across tools | **`X-Correlation-Id`** | Already on API; wire into Sentry/PostHog events |
| Transactional email | **Resend** (alt: Azure Communication Services Email) | Replace logging stub on booking/invite notifications |
| SMS + WhatsApp Business API | **Twilio** | Adeni-originated reminders (opt-in); not a substitute for `wa.me` deep links |
| Mobile push | **FCM + APNs** (typically via Firebase) | When Flutter consumer ships device registration |
| Maps / Explore UI | **Mapbox GL** (already in Discover) | Token in env; deepen geocoding later |
| Object storage | **Azure Blob** (`IFileStorage`) | Done for staging/prod path |
| Public image delivery | **Azure CDN or Front Door** in front of public blob (`Storage:AzureBlob:PublicBaseUrl`) | Staging/prod before heavy Discover traffic |

Ops detail: [observability.md](./observability.md). Media detail: [media-storage.md](./media-storage.md).

## Why App Insights *and* Sentry

They are complementary — one does not replace the other for Adeni’s surfaces:

| | Application Insights | Sentry |
|--|----------------------|--------|
| **Owns** | API / Azure infra, dependency health, SLO alerts | Angular/Flutter crashes, release regressions, source maps |
| **Does not own** | Polished client stack-trace workflow | Azure ops dashboards and availability probes |

**Rollout order if capacity is tight:** App Insights on API → Sentry on Discover → then remaining clients. Do not page on-call from both tools for the same failure mode (see [observability.md](./observability.md) alert ownership).

**Do not** add Datadog (or similar APM) while App Insights + Sentry cover the dual stack unless this policy is revised with a concrete gap.

## Why PostHog (not FullStory / Clarity alone)

PostHog is the **V1 product-learning** tool: funnels (discover → profile → book → pay), feature flags later if needed, and session replay in one product.

| Deferred / not V1 default | Reason |
|---------------------------|--------|
| **FullStory** | Strong replay; usually higher cost for early Ottawa/Lagos volume |
| **Microsoft Clarity** | Fine free heatmap/replay; weaker product analytics — optional supplement, not the system of record |
| **LogRocket** | Overlaps Sentry + replay; avoid a third error/replay stack |
| **Hotjar** | UX research only; use if you need surveys, not as analytics SoR |

## Notifications (email / SMS / WhatsApp / push)

**Current state:** Domain events + `INotificationDispatcher` + tenant preferences exist; delivery is still a **logging stub**. WhatsApp in-product is largely **deep links** (`wa.me`), not Cloud API sends.

**Decision — do not force one vendor for all channels.** No single platform is best-in-class for email + SMS + WhatsApp API + iOS/Android push.

| Channel | Vendor | Role |
|---------|--------|------|
| **Email** | **Resend** (preferred) or **ACS Email** | Booking/invite/receipt transactional mail |
| **SMS** | **Twilio** | Reminders where SMS is preferred |
| **WhatsApp API** | **Twilio** (or Meta Cloud API later) | Adeni→user templates (opt-in); compliance-aware |
| **Push** | **FCM + APNs** | Flutter consumer; separate from Twilio |

**Architecture (non-negotiable):**

1. Keep **`INotificationDispatcher`** (and preference gating). Add provider adapters — do not call Twilio/Resend from controllers.
2. Continue **WhatsApp deep links** for “Message business” / share kit (no BSP required).
3. Use **WhatsApp Business API** only for Adeni-originated notifications (reminders, confirmations) when the user/tenant opted in.
4. **Push is its own channel** (`push` / `fcm`) — same booking events, fan-out by preference; not a replacement for SMS/WhatsApp in Lagos.

**PII:** Never log full phone/email/body; mask in provider metadata and Serilog.

**Out of scope for V1 notifications:** marketing drip campaigns, CRM suites (Intercom as optional later support), replacing in-app SignalR messaging.

## Maps (Mapbox)

**Decision:** Keep **Mapbox** as the Discover maps vendor.

| Area | Status |
|------|--------|
| Discover Explore map (`mapbox-gl`) | ✅ Wired; requires `mapboxAccessToken` (`pk.…`) in app environment |
| Env tokens in repo | Placeholders — set per developer / staging secrets; never commit secrets |
| Server-side Mapbox Geocoding / Places | ❌ Gap — address → lat/lng pipeline not in API yet |
| Directions / Matrix / isochrones | Out of V1 unless explicitly requested |
| Portal/admin full map UX | Not required to match Discover |

**When deepening geo:** prefer Mapbox Geocoding behind an Application port (e.g. `IGeocodingService`) so providers stay swappable; do not add Google Maps in parallel without revising this policy.

## Media CDN (Azure Blob + edge)

**Decision:** Store in **Azure Blob** (already via `IFileStorage`); serve public covers/logos/gallery through **Azure CDN or Azure Front Door**, exposing the edge URL as `Storage:AzureBlob:PublicBaseUrl` (see [media-storage.md](./media-storage.md)).

| Layer | Choice |
|-------|--------|
| Upload | Presigned SAS → blob (not through API body for large files) |
| DB | Storage keys only — never binary |
| Public read | CDN hostname (`media.adeni.app` or equivalent), not raw blob endpoint in prod |
| Private docs | Verification PDFs stay private + signed URLs |

**CDN provides:** edge cache for Ottawa/Lagos, better Discover/Core Web Vitals, lower Blob egress.  
**CDN alone does not:** resize/WebP — V1 may ship reasonably sized uploads; optional variants on upload later.

**Do not** introduce a second object store (S3, Cloudflare R2) while Azure Blob + CDN covers V1.

## Explicitly out of scope (V1)

| Tool | Why deferred |
|------|----------------|
| **Datadog** | Duplicates App Insights + Sentry on Azure for a small team |
| **Elasticsearch / Kibana (ELK)** | Ops logging/APM is App Insights; discovery search stays SQL (+ Redis) until QPS justifies a search extract |
| **Extra RUM layers** | Prefer PostHog for product UX; avoid App Insights browser RUM + PostHog + Sentry performance all on by default |
| **Segment / Amplitude** | PostHog covers early product analytics |
| **LaunchDarkly** | Use PostHog feature flags until complexity demands otherwise |

Revisit Elasticsearch only as a **discovery search** backend if SQL cannot meet geo/keyword load — not as the default log stack.

## PostHog rules (PII & surfaces)

- **Surfaces:** Discover first (public SEO journey); portal when studying business onboarding/booking ops. Admin optional / low priority.
- **Never capture:** full phone, email, message bodies, payment card fields, Auth0 tokens. Align with `PiiMasker` / Serilog policy.
- **Mask** inputs that collect PII; scrub URL query params that may contain emails or tokens.
- **Do not** use PostHog as the primary crash tracker — that remains Sentry.
- Prefer **staging project** separate from production; disable or no-op SDK in local Development unless debugging analytics.

## Agent / PR guardrails

- New vendor (ops, analytics, notifications, maps, media) → must match this matrix; update this file in the same PR.
- Prefer Application ports (`INotificationDispatcher`, `IFileStorage`, future `IGeocodingService`) over calling SaaS from controllers/components.
- Prefer `@adeni/` shared wiring (api-client correlation, thin app wrappers) over copy-pasted DSNs per feature.
- Spec non-trivial provider wiring under `docs/specs/` when past a one-line env toggle.
- Never commit Mapbox/Twilio/Resend/Sentry/PostHog secrets.

## Related

| Doc | Role |
|-----|------|
| [observability.md](./observability.md) | Health, correlation, App Insights/Sentry ops detail |
| [media-storage.md](./media-storage.md) | Blob paths, upload flow, CDN URL convention |
| [product-direction.md](./product-direction.md) | V1 product authority |
| [sprints.md](./sprints.md) | Sprint 20 deploy + observability |
| [AGENTS.md](../AGENTS.md) | Agent playbook |
