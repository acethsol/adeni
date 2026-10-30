# A4 — Admin authorization (review)

**Launch posture:** Single **Adeni Admin** role (`AdeniRoles.Admin`) with MFA policy when `Auth0:RequireMfaForAdmin=true` remains **accepted for V1** unless product splits operational duties.

## Recommendation (future scoped roles)

| Scoped role | Example surfaces |
|-------------|------------------|
| PlatformAdmin | businesses, markets, entitlements catalog |
| FinanceAdmin | transactions, GMV, pricing mutations |
| TrustSafetyAdmin | verification, reviews, trust badges |
| PrivacyAdmin | customer privacy, exports, legal hold |
| SupportAdmin | read-heavy business/customer lookup |

**No new roles shipped in this branch.** Backend continues to require Admin JWT on `/admin/*`; audit middleware logs mutations.

## MFA / audit / rate limits

- Sensitive exports and privacy mutations: verify `AdminMfaPolicy` + audit entries (metadata only) in security pass — see AGENTS.md.
- Rate limiting: ASP.NET in-memory limiters remain **per instance** (accepted); edge aggregation deferred.
