# Billing State Machine

Suggested:
onboarding → trialing → active → past_due → grace → suspended

Other exits:
- trialing → cancelled
- active → cancelled
- suspended → active after recovery

Recommended default grace period: 7 days, but configurable.

Suspension must preserve tenant data and billing/audit history.
