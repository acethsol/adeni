# Spec: Sprint 36 — Messaging rewrite (conversations + SignalR)

> Durable SQL conversations with participant-based identity; SignalR for realtime; Redis / Service Bus only when scale demands it.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 36 — Messaging rewrite |
| **Author** | Cloud Agent |
| **Status** | Draft (awaiting approval) |
| **Created** | 2026-10-17 |
| **Supersedes** | [sprint-18-messaging.md](./sprint-18-messaging.md) persistence + delivery model (WhatsApp links + entitlement keep) |

---

## 1. Goal (one sentence)

Replace Sprint 18’s poll-only `MessageThread` model with **Conversation / Message / ConversationParticipant**, where **SQL Server is the source of truth** and **SignalR delivers realtime**, without forcing Redis onto the hot path.

---

## 2. Context

### Today (Sprint 18 on `dev`)

- Tables: `messaging.message_threads`, `messaging.messages`
- Unread = counters on the thread (`BusinessUnreadCount` / `CustomerUnreadCount`)
- Sender = `MessageSenderType` + `SenderAuth0Sub` string
- Portal inbox polls; no SignalR
- Customers can create/send; business reply gated by Pro (`subscription.messaging_required`)
- WhatsApp = `wa.me` deep links (no WABA); FAQ auto-responder posts as business-side text
- Notification prefs exist; delivery is still a logging stub; **no “new message” notification**

### Why rewrite

| Pain | Fix |
|------|-----|
| Thread counters break with multiple staff in one inbox | Per-participant read cursor |
| Polling feels stale | SignalR fan-out after durable write |
| Auth0-sub-only sender is hard to display / authorize for staff | Explicit participant rows linked to `Customer` / `BusinessUser` |
| No attachment / context model room | First-class context + attachment metadata |
| No idempotent send | Client message id unique per conversation |

### Design principle (locked)

```
Angular / Flutter
       │
       ├── REST (.NET)     → history, create, mark read, attachments
       └── SignalR hub     → live MessageCreated / ReadUpdated
              │
       Messaging module
              │
      ┌───────┴────────┐
      ▼                ▼
 SQL Server         SignalR
 (truth)            (ephemeral delivery)
      │
      ▼
 Notifications (async; after commit)
```

**Redis** = optional later (SignalR backplane / presence).  
**Service Bus** = optional later (notification workers).  
**Blob Storage** = attachment bytes (metadata in SQL).

---

## 3. Identity model (elaborated)

### 3.1 The problem with `SenderUserId`

Adeni does **not** have a single shared `User` table.

| Population | Table | Key | Scope |
|------------|-------|-----|-------|
| Customer (discover / book / chat) | `identity.customers` | `Id` + `Auth0Sub` | Platform-wide |
| Portal staff / owner | `identity.business_users` | `Id` + `Auth0Sub` + `TenantId` | Per tenant |
| Bot / system | *(none today)* | — | FAQ, future Ask Adeni |

A bare `Message.SenderUserId : Guid` is **ambiguous**: the same Guid space is not shared, and Auth0 `sub` strings are not Guids. Sprint 18’s `SenderAuth0Sub` works for humans but:

- Cannot cleanly represent bots
- Forces every query to re-resolve “who is this?” for display names
- Makes “which staff replied?” dependent on joining `BusinessUsers` by string every time
- Does not support per-person read state (owner vs receptionist)

### 3.2 Decision: Messaging **Participant** is the actor handle

Inside the messaging module, every speaker/reader is a **`ConversationParticipant`** row. Messages point at **participants**, not raw Auth0 strings or overloaded user Guids.

```
Auth0 JWT (sub)
    │
    ├─ resolve Customer by Auth0Sub          → Customer.Id
    └─ resolve BusinessUser by Auth0Sub+Tenant → BusinessUser.Id
            │
            ▼
    ConversationParticipant  (stable Id inside messaging)
            │
            ▼
    Message.SenderParticipantId
```

### 3.3 Participant types

```csharp
public enum ConversationParticipantType
{
    Customer = 0,
    BusinessUser = 1,
    Bot = 2,
}
```

| Type | Required FKs / keys | Auth |
|------|---------------------|------|
| `Customer` | `CustomerId` | JWT `sub` → `Customer.Auth0Sub` |
| `BusinessUser` | `BusinessUserId` (+ tenant) | JWT `sub` → `BusinessUser.Auth0Sub` for that tenant |
| `Bot` | `BotKey` (e.g. `faq`, `adeni-assistant`) | No human login; only messaging services may post |

**Invariant:** exactly one of `CustomerId` / `BusinessUserId` / `BotKey` is set per type.

Optional denormalized `Auth0Sub` on the participant row is allowed for fast authz checks, but **authorization always re-validates** against `Customer` / `BusinessUser` (subs can change rarely; prefer Id FKs as truth).

### 3.4 What goes on `Message`

```text
SenderParticipantId  → ConversationParticipant.Id   (required)
```

Do **not** store a free-floating `SenderUserId`.  
Do **not** rely only on Auth0 sub on the message row (may keep as denormalized audit copy if useful).

### 3.5 Conversation membership

| Role on conversation | How created |
|----------------------|-------------|
| Customer | On conversation create (the customer party) |
| Business users | Lazy: first portal open/reply upserts participant for that `BusinessUser`; or auto-add all users with `portal.messages` (choose **lazy upsert** for v1) |
| Bot | Upserted when FAQ/LLM posts |

Unread for a user = messages after that participant’s `LastReadMessageId` (or `LastReadAt`), **not** a counter on the conversation.

### 3.6 Display names

Resolved at read time (or cached on projection):

- Customer → `Customer.Name`
- BusinessUser → staff display name if `StaffMemberId` set, else role / email / “Team”
- Bot → fixed labels (“FAQ Assistant”, “Ask Adeni”)

### 3.7 Rejected alternatives

| Alternative | Why not |
|-------------|---------|
| Single global `Users` table now | Large unrelated migration; customers ≠ staff |
| Auth0 sub as sole sender key | Weak FK, bad for bots, awkward joins |
| Only `CustomerId` + “business blob” | Cannot attribute which staff replied or read |
| `StaffMemberId` as sender | Staff without portal invite cannot / should not send; portal identity is `BusinessUser` |

### 3.8 AuthZ summary

| Action | Rule |
|--------|------|
| Customer send / read | Participant type Customer and `Customer.Auth0Sub == sub` |
| Business send / read | Participant type BusinessUser, same tenant, Pro messaging entitlement, `portal.messages` |
| Bot send | Internal service only (FAQ / future LLM), never via public hub invoke |
| Cross-tenant | Impossible — conversation is `ITenantEntity`; EF tenant filter |

---

## 4. Schema (SQL Server, schema `messaging`)

### 4.1 `conversations`

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier | PK |
| TenantId | uniqueidentifier | FK tenants; **not** `BusinessId` |
| CustomerId | uniqueidentifier | FK customers |
| ContextType | tinyint / string | `None`, `Booking`, `Quote`, `Profile` |
| ContextId | uniqueidentifier NULL | Booking/quote id when set |
| Status | tinyint | Open / Closed |
| CreatedAt | datetimeoffset | |
| LastMessageAt | datetimeoffset | |
| CreatedByParticipantId | uniqueidentifier NULL | |
| RowVersion | rowversion | concurrency |

**Indexes**

- `(TenantId, LastMessageAt DESC)` — inbox list
- `(TenantId, CustomerId, ContextType, ContextId)` — find-or-create
- Unique filtered: one open conversation per `(TenantId, CustomerId, ContextType, ContextId)` (NULL context → profile thread)

### 4.2 `conversation_participants`

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier | PK — **messaging actor id** |
| ConversationId | uniqueidentifier | FK |
| TenantId | uniqueidentifier | denormalized for filters |
| ParticipantType | tinyint | Customer / BusinessUser / Bot |
| CustomerId | uniqueidentifier NULL | |
| BusinessUserId | uniqueidentifier NULL | |
| BotKey | nvarchar(64) NULL | |
| Auth0Sub | nvarchar(128) NULL | denormalized optional |
| LastReadMessageId | uniqueidentifier NULL | |
| LastReadAt | datetimeoffset NULL | |
| IsMuted | bit | default 0 |
| JoinedAt | datetimeoffset | |

**Indexes / constraints**

- Unique `(ConversationId, CustomerId)` where Customer
- Unique `(ConversationId, BusinessUserId)` where BusinessUser
- Unique `(ConversationId, BotKey)` where Bot
- `(ConversationId)` include read cursor cols

### 4.3 `messages`

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier | PK |
| ConversationId | uniqueidentifier | FK |
| TenantId | uniqueidentifier | |
| SenderParticipantId | uniqueidentifier | FK participants |
| Type | tinyint | Text / System / AttachmentOnly (v1 mostly Text) |
| Body | nvarchar(4000) NULL | required for Text |
| ClientMessageId | nvarchar(64) NULL | **idempotency** |
| CreatedAt | datetimeoffset | |
| DeliveredAt | datetimeoffset NULL | optional; set when hub ack / first delivery |
| DeletedAt | datetimeoffset NULL | soft-delete |
| ModerationStatus | tinyint | None / Hidden / Flagged |

**Indexes**

- Unique `(ConversationId, ClientMessageId)` where `ClientMessageId IS NOT NULL`
- `(ConversationId, CreatedAt, Id)` — **keyset pagination**
- `(TenantId, CreatedAt)` — moderation / admin later

### 4.4 `message_attachments`

| Column | Type | Notes |
|--------|------|-------|
| Id | uniqueidentifier | PK |
| MessageId | uniqueidentifier | FK |
| BlobUrl / BlobKey | nvarchar(512) | storage key; public URL via existing media pipeline |
| ContentType | nvarchar(128) | |
| FileName | nvarchar(256) | |
| FileSize | bigint | |
| CreatedAt | datetimeoffset | |

Binary in **Azure Blob / local media** (same as gallery/avatars). SQL holds metadata only.

---

## 5. Write path (durable first)

```
1. Authorize caller → resolve Customer or BusinessUser
2. Ensure ConversationParticipant row (lazy upsert for business users)
3. INSERT Message with ClientMessageId (idempotent)
4. UPDATE Conversation.LastMessageAt
5. COMMIT
6. SignalR: MessageCreated to conversation group
7. Domain event / outbox → Notifications (email/push prefs; stub OK until adapters land)
```

**Never** notify or fan-out before commit.  
**Retries:** same `ClientMessageId` returns the existing message (200), no duplicate.

---

## 6. SignalR hub

Hub: `/hubs/messaging` (Auth0 JWT; tenant claim / header for business).

| Client → Server | Server → Client |
|-----------------|-----------------|
| `JoinConversation(conversationId)` | `MessageCreated(dto)` |
| `LeaveConversation(conversationId)` | `ReadUpdated(participantId, lastReadMessageId)` |
| `SendMessage(conversationId, body, clientMessageId)` *(optional thin wrapper over REST)* | `ConversationUpdated(summary)` |
| `MarkRead(conversationId, messageId)` | |

**v1 recommendation:** keep **REST as the write API** (easier idempotency, attachments, tests); SignalR primarily for **push**. Optional hub `SendMessage` may call the same application service.

Groups: `conversation:{id}`, plus user groups `customer:{id}` / `business-user:{id}` for inbox badges.

**Scale-out later:** Redis backplane when running multiple API instances — not required for single-node staging.

---

## 7. REST API (v2 under messaging rewrite)

Prefer new routes; keep Sprint 18 routes as deprecated shims or migrate in place with breaking change behind feature flag.

| Audience | Routes |
|----------|--------|
| Customer | `POST/GET /api/v1/messages/conversations`, `GET .../{id}`, `POST .../{id}/messages`, `POST .../{id}/read`, `GET .../{id}/messages?afterId=&take=` |
| Business | `GET /api/v1/tenant/messages/conversations`, `.../{id}`, send, read, unread-count, templates, settings |
| Public | Keep WhatsApp link endpoints from Sprint 18 |

Unread-count = aggregate over participant cursors for that BusinessUser (or tenant-wide for badge: messages where any business participant is behind).

Entitlement: unchanged — Free cannot use business inbox/reply.

---

## 8. Clients

| Client | Work |
|--------|------|
| Portal Angular `/messages` | Rewrite inbox to conversations + SignalR; keep WhatsApp tab |
| Discover / Flutter | Customer conversation UI can follow; WhatsApp CTA remains |
| Shared + api-client | New schemas; deprecate thread schemas |

---

## 9. Migration from Sprint 18

1. Add new tables alongside `message_threads` / `messages`
2. Backfill:
   - Thread → Conversation (`ContextType=Booking|None`, `ContextId=BookingId`)
   - Create Customer participant + synthetic “legacy business” Bot or first Owner BusinessUser participant
   - Messages → map `SenderAuth0Sub` → Customer or BusinessUser participant
   - Approximate `LastReadMessageId` from unread counts (best-effort) or reset cursors to latest
3. Dual-read feature flag → cut over writes → remove old tables in a follow-up sprint

---

## 10. In scope

- [ ] **36a** Schema: conversations, participants, messages, attachments (+ indexes, soft-delete)
- [ ] **36b** Application services: create/find conversation, idempotent send, keyset history, mark-read via participant cursor
- [ ] **36c** Identity resolution: Auth0 → Customer / BusinessUser → Participant (document + enforce)
- [ ] **36d** SignalR hub + portal subscription (MessageCreated, ReadUpdated, unread badge)
- [ ] **36e** Portal inbox rewrite (Angular)
- [ ] **36f** Domain event after commit for notifications (prefs-aware; stub dispatcher OK)
- [ ] **36g** Attachment upload metadata + blob (reuse media upload patterns); images only in v1
- [ ] **36h** Migration / backfill from Sprint 18 threads
- [ ] **36i** Tests: idempotency, tenant isolation, participant authz, keyset paging, hub fan-out (integration)
- [ ] **36j** Docs: this spec → Implemented; update architecture + Sprint 18 “superseded by 36”

---

## 11. Out of scope

- WhatsApp Business API / WABA webhooks (keep `wa.me` adapter)
- Redis backplane / presence (until multi-instance)
- Azure Service Bus workers (until notification adapters are real)
- Flutter customer inbox (can track as 36+ / mobile sprint)
- Typing indicators / reactions (optional thin SignalR later)
- LLM replies as product feature (Sprint 20); when added, post as `Bot` participant
- Changing subscription entitlement rules

---

## 12. Acceptance criteria

- [ ] Send persists to SQL before any client receives SignalR `MessageCreated`
- [ ] Duplicate `ClientMessageId` does not create a second row
- [ ] Two BusinessUsers in the same conversation can have different unread states
- [ ] Message sender is always a Participant; bots use `BotKey`, never a fake Auth0 sub
- [ ] Portal inbox updates live without refresh when another party sends
- [ ] Free tier still blocked on business messaging APIs
- [ ] History uses keyset pagination (no deep `OFFSET`)
- [ ] Attachments: blob bytes + SQL metadata; message remains readable if blob missing
- [ ] Sprint 18 data migrated or explicitly dual-run with flag

---

## 13. Sequencing

| Step | Depends on |
|------|------------|
| Spec approval | — |
| 36a–c + tests | Approval |
| 36d–e portal + SignalR | 36b |
| 36f notifications hook | 36b |
| 36g attachments | Media upload patterns |
| 36h migration | 36a–b stable |
| Redis / Service Bus | Multi-instance or real push/email adapters |

Fits after monetization track (33/34) or parallel if staffing allows — messaging rewrite does not block Connect/split, but competes for API/portal attention.

---

## 14. Open questions (for approval)

1. **Lazy vs eager business participants** — v1 default: lazy upsert on first read/reply. OK?
2. **Breaking API** — new `/conversations` routes vs in-place replace of `/threads`?
3. **Migration cursor reset** — acceptable to zero unread on cutover if backfill is lossy?
4. **Hub send** — REST-only writes in v1, or also hub `SendMessage`?
