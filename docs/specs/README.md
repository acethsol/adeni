# Feature specs

Write a spec **before** implementation. Agents and humans use the same template.

## Workflow

1. Copy [`_template.md`](./_template.md) → `docs/specs/sprint-XX-short-name.md`
2. Fill sections 1–7; mark **Out of scope** explicitly
3. Run standing play 1 from [AGENTS.md](../../AGENTS.md) or review with the team
4. Set status to **Approved**, then implement (standing play 2)
5. After merge, set status to **Implemented**

## Naming

`docs/specs/sprint-17-paystack-deposits.md` — sprint number + kebab-case slug.

## When to skip

Trivial one-line fixes (typo, comment) do not need a spec. Anything touching API contracts, auth, tenant data, payments, or a new module does.
