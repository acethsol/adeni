# Trials, Discounts & Price Protection

## Default trial
- 30 days
- Professional entitlements
- starts when business is activated/published, not when tenant record is merely created

## Billing statuses
Support:
- trialing
- active
- past_due
- grace
- suspended
- cancelled

No silent free tier after trial.

## Price protection
Each subscription must retain:
- pricing version
- agreed monthly amount
- agreed currency
- effective date
- optional price-lock end date

Rule:
> Price changes apply prospectively. Existing customers are never repriced without notice and without honoring any price-lock period.

## Founding offers
Support founding-customer pricing as an override/discount, not a separate SKU.

Recommended operational default:
- 12-month price lock
- configurable in Admin
- do not promise lifetime pricing

## Admin commercial controls
Per tenant:
- trial on/off
- trial days
- trial SKU
- subscription SKU
- billing status
- custom monthly SaaS amount
- custom currency
- percentage discount
- fixed discount
- N discount cycles
- effectiveFrom
- expiresAt
- priceLockEndsAt
- comp reason
- approvedBy
- audit history

All commercial mutations require audit logging.
