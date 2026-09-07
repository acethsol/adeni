# Sprint 19 — CAC verification research (19g)

| Field | Value |
|-------|-------|
| **Status** | Research stub |
| **Created** | 2026-09-07 |

---

## Goal

Evaluate options for automated Corporate Affairs Commission (CAC) verification for Nigerian businesses on Adeni.

---

## Current state (Sprint 19)

- Businesses submit a **CAC registration reference** during onboarding or badge upgrade.
- Admins manually review pending businesses and grant the **CAC verified** badge.
- No live API integration with CAC or third-party registries.

---

## Options considered

| Approach | Pros | Cons |
|----------|------|------|
| **Manual admin review** (current) | Low cost, no external dependency | Slow, does not scale |
| **CAC public search scrape** | No formal API contract | Fragile, ToS/legal risk, brittle HTML |
| **Third-party KYB provider** (e.g. Smile ID, Youverify, Mono) | Structured API, audit trail | Per-check cost, vendor onboarding |
| **OpenCorporates / similar** | Good for cross-border | Incomplete NG coverage |

---

## Recommendation

1. **Short term:** Keep manual admin grant for Sprint 19–20; display CAC badge only after admin approval.
2. **Medium term:** Pilot one NG KYB provider with sandbox API for CAC lookup by RC number; store verification audit log.
3. **Long term:** Auto-grant CAC badge on successful API match; retain admin override/revoke.

---

## Integration sketch (future)

```
Business submits RC number
  → Adeni calls KYB provider /cac/verify
  → On match: auto-grant cac badge + store provider reference
  → On mismatch: leave pending + notify admin queue
```

---

## Out of scope for Sprint 19

- Vendor selection or contract
- Automated CAC API calls
- Pay-to-verify monetization

---

## References

- [CAC Public Search](https://search.cac.gov.ng/) — public registry (no official API documented)
- Sprint 19 spec: `docs/specs/sprint-19-trust-quotes.md`
