# MFA enforcement (SOC2-06) — operator summary

Full runbook: [Confluence — MFA Enforcement Runbook](https://aceth.atlassian.net/wiki/spaces/SD/pages/26247205)

## Day 1 checklist

| Platform | Action | Verify |
|----------|--------|--------|
| **Auth0** | Enable MFA; add Login Action for admin role | JWT contains `amr: mfa` for admin users |
| **Azure** | Conditional Access: require MFA for all users | `az rest --method GET --url https://graph.microsoft.com/v1.0/policies/conditionalAccessPolicies` |
| **GitHub** | Org `acethsol` → Require 2FA for all members | Settings → Organization → Authentication security |
| **Atlassian** | Enforce 2FA for all users | Admin → Security → Authentication policies |

## Code enforcement

- `Auth0:RequireMfaForAdmin=true` (default)
- API policy `AdminMfaPolicy` on `/api/v1/admin/*`
- Auth0 Login Action: see [auth0-setup.md](./auth0-setup.md)

## Evidence folder

Store screenshots + export logs in `SOC2-06-MFA/` for audit (see Confluence runbook § evidence table).
