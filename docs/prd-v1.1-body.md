# Adeni — Product Requirements Document (PRD v1)

> **Status:** Build specification | **Version:** 1.1 | **Parent:** [Adeni Product Bible](https://aceth.atlassian.net/wiki/spaces/SD/pages/26279937) | **Compliance:** [SOC 2 Compliance Framework v1](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170)

---

## 10. Non-Functional Requirements

### 10.1 General NFRs

| Area | Requirement |
| --- | --- |
| Performance | Discovery API p95 < 500ms; booking create p95 < 1s |
| Availability | 99.5% uptime target for MVP |
| Security | TenantId enforced on all tenant routes; OWASP top 10 baseline |
| Privacy | NDPR/GDPR data export/delete for customer (admin v1) |
| Mobile | Flutter responsive; min iOS 15, Android 8, modern browsers |
| Accessibility | WCAG 2.1 AA target for core flows |
| Localization | English only v1; NGN currency; Lagos geography |

### 10.2 SOC 2 Compliance NFRs

> Source: [SOC 2 Compliance Framework v1 §12](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247170)

| ID | Requirement | Acceptance criteria | TSC mapping |
| --- | --- | --- | --- |
| **SOC2-01** | All admin actions audit-logged with actor, timestamp, entity | Every `/admin/*` mutation writes to `admin.audit_logs` with adminId, action, entityType, entityId, correlationId, UTC timestamp | CC6, CC4 |
| **SOC2-02** | Cross-tenant data access attempts logged and alerted | Tenant middleware returns 403; structured log at Warning with userId, attemptedTenantId, route; App Insights alert on spike | CC6, CC4 |
| **SOC2-03** | PII masked in application logs | Phone, email, message body never appear in full in logs; Serilog destructuring policy masks PII fields | C1, P4 |
| **SOC2-04** | TLS 1.2+ on all endpoints; no HTTP fallback | HTTPS-only in staging/prod; HSTS header enabled; HTTP redirects to HTTPS | CC5, C1 |
| **SOC2-05** | Secrets stored in Key Vault only in staging/prod | Connection strings, API keys via Azure Key Vault + Managed Identity; zero secrets in repo | CC6, CC5 |
| **SOC2-06** | MFA enforced for admin and infrastructure accounts | Auth0 admin users require MFA; Azure/GitHub/Atlassian MFA enforced org-wide | CC6 |
| **SOC2-07** | Dependency vulnerability scan on every PR | CI runs `dotnet list package --vulnerable` + Dependabot; PR blocked on critical CVEs | CC7 |
| **SOC2-08** | Database backups with PITR; restore tested quarterly | Azure PostgreSQL PITR enabled (≥7 days); quarterly restore drill documented | A1, CC7 |
| **SOC2-09** | Customer data export/delete capability (admin v1) | Admin can export JSON bundle and initiate erasure; action audit-logged; 30-day purge job | P6, P8 |
| **SOC2-10** | Incident response plan tested annually | Tabletop exercise documented; on-call runbook current | CC7, CC4 |

### 10.3 Admin privacy endpoints (SOC2-09)

| Method | Endpoint | Description |
| --- | --- | --- |
| GET | `/admin/customers/{id}/export` | NDPR/GDPR data export (JSON bundle) |
| POST | `/admin/customers/{id}/delete` | Initiate customer erasure (30-day purge) |

### 10.4 Admin features added (§4.3)

| ID | Feature |
| --- | --- |
| P-07 | Customer data export (NDPR subject access) |
| P-08 | Customer data deletion (admin-initiated v1) |
