# Spec: Sprint 40.1 — Attendance realtime & presence cache

| Field | Value |
|-------|-------|
| **Sprint** | 40.1 |
| **Status** | Implemented |
| **Issue** | [#52](https://github.com/acethsol/adeni/issues/52) |
| **Authority** | [staff-attendance-verified-presence.md](./staff-attendance-verified-presence.md) |

## Goal

Reduce SQL reads for on-premises presence checks and refresh the business team attendance board live after attendance mutations.

## In scope

- Redis (or dev memory) cache: `tenant:{tenantId}:presence:{businessUserId}` → `{ sessionId, businessLocationId, expiresAt }`; write-through on verify / invalidate / clock-in; read in `OnPremisesSensitiveAccessPolicy` with SQL fallback on miss; TTL = remaining validity; no coordinates in cache.
- SignalR: `AttendanceTeamChanged` on tenant operations hub after attendance writes; team attendance page refetches Today roster when connected.

## Out of scope

- Flutter/mobile attendance, exceptions board, messaging presence gate.

## Test plan

- Integration: cache populated on clock-in, cleared on clock-out; sensitive access still works when cache empty (SQL).
- `dotnet test Adeni.slnx -c Release`
- Business portal team page smoke with hub connected.
