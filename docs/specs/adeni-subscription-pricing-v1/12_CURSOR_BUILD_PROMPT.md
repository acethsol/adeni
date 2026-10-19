# Cursor Build Prompt

Use this package as the source of truth for Adeni subscription and pricing.

Context:
- .NET backend
- SQL Server
- Angular web
- Beauty & Wellness
- Ottawa/Canada + Lagos/Nigeria launch
- no permanent free plan
- 30-day Professional trial
- CA launch pricing: 99 / 149 / 249 CAD
- NG launch pricing: 35,000 / 55,000 / 95,000 NGN
- launch Adeni GMV fee: 0%
- non-custodial customer payments

Before coding:
1. Inspect existing subscription, tenant, pricing, payment, location, and admin code.
2. Identify what already exists.
3. Produce a short implementation plan mapping this spec to the repository.
4. Preserve working behavior.
5. Do not create duplicate tables/concepts if existing ones can be extended safely.
6. Flag conflicts before large refactors.

Rules:
- keep SaaS subscription billing separate from customer service payments
- backend entitlement enforcement is authoritative
- centralize pricing and entitlements
- preserve historical agreed prices
- never silently reprice existing tenants
- audit all Admin commercial overrides
- do not store raw card data
- do not add Adeni surcharge to customer checkout
- do not enable non-zero GMV fee in this launch build
- do not implement future add-ons unless explicitly requested
- add automated tests

Deliver changes incrementally.
