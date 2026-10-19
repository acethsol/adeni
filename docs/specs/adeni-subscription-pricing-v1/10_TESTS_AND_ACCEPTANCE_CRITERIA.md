# Tests & Acceptance Criteria

## Pricing
- CA resolves CAD launch pricing.
- NG resolves NGN launch pricing.
- city does not change price within same country.
- existing tenant keeps bound price when a new catalog is activated.
- disabled country cannot self-serve.

## Trial
- activated tenant gets 30-day Professional trial.
- tenant creation alone does not consume trial.
- admin can alter/end trial.
- trial expiry never creates a permanent free plan.

## Entitlements
Starter:
- max 1 location
- max 3 practitioners
Professional:
- max 1 location
- max 10 practitioners
Business:
- multi-location supported

Core booking/messaging/payment/invoice features remain available according to matrix.

All limits enforced server-side.

## Overrides
- admin can set negotiated price with reason
- effective/expiry dates work
- audit history written
- catalog change does not overwrite agreed price

## Discounts
Test percentage, fixed, N cycles, expiry, currency consistency, no negative effective price.

## GMV fee
Launch:
- CA = 0%
- NG = 0%
- customer total equals business-defined total
- no Adeni surcharge

## Billing country
Changing primary operational location after subscription start does not silently change billing country/currency.

## Security
- tenant cannot alter agreed price through client payload
- non-admin cannot mutate commercial overrides
