# Spec: Sprint 36 — Messaging rewrite (conversations + SignalR)

> Durable SQL conversations with participant-based identity; SignalR for realtime; Redis / Service Bus only when scale demands it.

| Field | Value |
|-------|-------|
| **Sprint** | Sprint 36 — Messaging rewrite |
| **Author** | Cloud Agent |
| **Status** | ✅ Done (wrapped) |
| **Created** | 2026-10-17 |
| **Replaces** | [sprint-18-messaging.md](./sprint-18-messaging.md) — **full teardown**, no data migration |

---

## 1. Goal (one sentence)

**Remove Sprint 18 messaging entirely** and rebuild with **Conversation / Message / ConversationParticipant**, where **SQL Server is the source of truth** and **SignalR delivers realtime**, without forcing Redis onto the hot path.

---

## 2. Context

### Today (Sprint 18 on `dev`) — **to be deleted**

- Tables: `messaging.message_threads`, `messaging.messages`
- Unread = counters on the thread (`BusinessUnreadCount` / `CustomerUnreadCount`)
- Sender = `MessageSenderType` + `SenderAuth0Sub` string
- Portal inbox polls; no SignalR
- Customers can create/send; business reply gated by Pro (`subscription.messaging_required`)
- WhatsApp = `wa.me` deep links (no WABA); FAQ auto-responder posts as business-side text
- Notification prefs exist; delivery is still a logging stub; **no “new message” notification**

**Cutover policy (locked):** no backfill, no dual-run, no shims. Drop Sprint 18 messaging schema/code/tests/UI contracts and ship the new module clean. Existing in-app threads/messages are discarded. WhatsApp deep-link *product need* stays, but is re-implemented under the new module (not left as Sprint 18 leftovers).

### Why rebuild fresh

| Pain | Fix |
|------|-----|
| Thread counters break with multiple staff in one inbox | Per-participant read cursor |
| Polling feels stale | SignalR fan-out after durable write |
| Auth0-sub-only sender is hard to display / authorize for staff | Explicit participant rows linked to `Customer` / `BusinessUser` |
| No attachment / context model room | First-class context + attachment metadata |
| No idempotent send | Client message id unique per conversation |
| Migrating lossy unread counters isn’t worth it | Greenfield tables; accept empty inbox at cutover |

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
| Business users | **Lazy upsert (locked):** first portal open/reply creates participant for that `BusinessUser` |
| Bot | Upserted when FAQ/LLM posts |

**No assignment (locked):** there is no `AssignedBusinessUserId`, claim, or “first responder” routing. Whoever opens/replies first is just the first attributed staff participant — not an owner of the thread.

### 3.5.1 Shared inbox vs personal cursor (locked)

| Concern | Rule |
|---------|------|
| **Who sees the list** | Every BusinessUser with `portal.messages` sees the **same tenant conversation list** (shared inbox). Lazy does **not** hide chats from owner/admin. |
| **Who is a participant** | Upserted on first open/reply — used for send attribution and **personal** read cursor |
| **Portal unread badge (team)** | Count conversations that need attention for the **business**, not only chats you’ve personally opened: customer (or bot-to-customer) activity after the latest **any-business-participant** read; if no business participant exists yet, any customer message counts as needing attention |
| **After you open** | Upsert your participant; `MarkRead` advances **your** `LastReadMessageId` so “I’ve read this” is personal |
| **List row hint** | May show last staff replier name when a business participant has sent; still visible to everyone |

So: owner is never blinded; badge stays useful for the whole desk; cursors stay per-person once you’ve engaged.

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
| `MarkRead(conversationId, messageId)` *(optional; prefer REST)* | `ConversationUpdated(summary)` |

**Locked:** **REST is the only write path** for send (idempotency, attachments, tests). SignalR is **push only** — no hub `SendMessage` in v1.

Groups: `conversation:{id}`, plus user groups `customer:{id}` / `business-user:{id}` for inbox badges.

**Scale-out later:** Redis backplane when running multiple API instances — not required for single-node staging.

---

## 7. REST API (new module — no Sprint 18 shims)

**Locked:** new `/conversations` routes only. Delete all `/threads` (and related Sprint 18 message) endpoints; do not leave deprecated aliases.

| Audience | Routes |
|----------|--------|
| Customer | `POST/GET /api/v1/messages/conversations`, `GET .../{id}`, `POST .../{id}/messages`, `POST .../{id}/read`, `GET .../{id}/messages?afterId=&take=` |
| Business | `GET /api/v1/tenant/messages/conversations`, `.../{id}`, send, read, unread-count, templates, settings |
| Public / booking | Re-implement WhatsApp link endpoints under the new module (`GET /api/v1/businesses/{slug}/whatsapp-link`, `GET /api/v1/bookings/{id}/whatsapp-link`) |

**Unread-count (business, locked):** **team** badge — conversations needing attention for the tenant (see §3.5.1), same number for all `portal.messages` users. Personal cursor still drives per-open “caught up” state after mark-read.

Entitlement: unchanged — Free cannot use business inbox/reply.

---

## 8. Clients

| Client | Work |
|--------|------|
| Portal Angular `/messages` | **Replace** Sprint 18 inbox with conversations + SignalR; WhatsApp tab stays as product surface |
| Discover / Flutter | Remove any Sprint 18 thread client usage; WhatsApp CTA + later conversation UI |
| Shared + api-client | **Delete** thread schemas/methods; add conversation schemas/methods |

---

## 9. Sprint 18 teardown (no migration)

**Locked:** greenfield rebuild. Do **not** backfill threads/messages. Inboxes start empty after deploy.

### Remove completely

| Layer | Delete / replace |
|-------|------------------|
| SQL | Drop `messaging.message_threads`, `messaging.messages` (and related indexes/FKs) |
| Domain | `MessageThread`, Sprint 18 `Message`, enums tied only to that model |
| Application | `IMessageThreadService` and Sprint 18 DTOs (`ThreadSummaryResponse`, etc.) |
| Infrastructure | `MessageThreadService`, EF configs for old tables |
| API | Controllers/actions for `/messages/threads`, tenant thread routes; replace WhatsApp link handlers with new-module versions |
| Shared / api-client | `messageThread*` schemas and client methods |
| Portal | Polling thread inbox UI |
| Tests | `MessagingIntegrationTests` (rewrite against new module) |
| Docs | Mark Sprint 18 **superseded / removed by Sprint 36** |

### Keep / re-home (not Sprint 18 leftovers)

- Messaging **entitlement** (`SubscriptionEntitlements.Messaging`) — still gates business inbox
- `portal.messages` permission
- Notification **preferences** table/API (channel toggles) — wire “new message” events from the new module later
- FAQ auto-responder **behavior** — re-implement posting as a `Bot` participant
- WhatsApp **product** (`wa.me` links, templates tab) — rebuild against new services

---

## 10. Decisions locked

| Decision | Choice |
|----------|--------|
| Business participants | **Lazy upsert** on first open/reply |
| Inbox visibility | **Shared tenant list** for all `portal.messages` — owner not blinded |
| Unread badge | **Team unread** (needs attention for the business); personal cursor after open |
| Assignment / claim | **None** — no assigned owner or first-responder routing |
| API shape | **New `/conversations` routes only**; delete `/threads` |
| Old data | **No migration** — drop Sprint 18 tables/data |
| Unread at cutover | N/A (empty inbox); new cursors start clean |
| Writes | **REST-only**; SignalR push-only |

---

## 11. In scope

- [x] **36a** Teardown: remove Sprint 18 messaging schema, services, APIs, client contracts, portal thread inbox, obsolete tests
- [x] **36b** Schema: conversations, participants, messages, attachments (+ indexes, soft-delete)
- [x] **36c** Application services: create/find conversation, idempotent send, keyset history, mark-read via participant cursor
- [x] **36d** Identity resolution: Auth0 → Customer / BusinessUser → Participant (lazy business upsert)
- [x] **36e** SignalR hub + portal subscription (MessageCreated, ConversationUpdated) — push only
- [x] **36f** Portal inbox rewrite (Angular) on `/conversations`
- [x] **36g** Domain event after commit for notifications (prefs-aware; stub dispatcher OK)
- [x] **36h** Attachment upload metadata + blob (reuse media upload patterns); images only in v1
- [x] **36i** Re-home WhatsApp links + templates + FAQ-as-Bot under the new module
- [x] **36j** Tests: idempotency, tenant isolation, participant authz, keyset paging
- [x] **36k** Docs: this spec → Implemented; Sprint 18 marked removed

---

## 12. Out of scope

- Migrating or preserving Sprint 18 thread/message rows
- Deprecated `/threads` compatibility shims
- WhatsApp Business API / WABA webhooks (`wa.me` adapter only)
- Redis backplane / presence (until multi-instance)
- Azure Service Bus workers (until notification adapters are real)
- Flutter customer inbox (can track as 36+ / mobile sprint)
- Typing indicators / reactions (optional thin SignalR later)
- Hub `SendMessage` (v1 REST-only writes)
- Conversation **assignment** / claim / round-robin routing
- LLM replies as product feature (Sprint 20); when added, post as `Bot` participant
- Changing subscription entitlement rules

---

## 13. Acceptance criteria

- [x] No Sprint 18 messaging types, tables, `/threads` routes, or thread client APIs remain
- [x] Send persists to SQL before any client receives SignalR `MessageCreated`
- [x] Duplicate `ClientMessageId` does not create a second row
- [x] Two BusinessUsers can have different personal read cursors after both have opened
- [x] Owner/admin with `portal.messages` can list and open a chat only a receptionist has replied to
- [x] Team unread badge counts chats with no business read yet (and after latest business read)
- [x] Message sender is always a Participant; bots use `BotKey`, never a fake Auth0 sub
- [x] Portal inbox updates live without refresh when another party sends
- [x] Free tier still blocked on business messaging APIs
- [x] History uses keyset pagination (no deep `OFFSET`)
- [x] Attachments: blob bytes + SQL metadata; message remains readable if blob missing
- [x] WhatsApp link + template surfaces still work after re-home
- [x] FAQ replies appear as Bot participant messages

---

## 14. Sequencing

| Step | Depends on |
|------|------------|
| Spec approval | — **done** |
| 36a teardown (same change-set as 36b once new schema lands is fine) | Approval |
| 36b–d + tests | Empty messaging surface after teardown |
| 36e–f portal + SignalR | 36c |
| 36g notifications hook | 36c |
| 36h attachments | Media upload patterns |
| 36i WhatsApp + FAQ re-home | 36c |
| Redis / Service Bus | Multi-instance or real push/email adapters |

Fits after monetization track (33/34) or parallel if staffing allows — messaging rewrite does not block Connect/split, but competes for API/portal attention.

---

## 15. Security & design wrap-up (done)

Aligned with AGENTS.md non-negotiables and portal design system:

| Control | How |
|---------|-----|
| Tenant isolation | `ITenantEntity` + conversation membership checks; hub join validates customer or same-tenant business |
| Messaging entitlement | REST list/get/send/mark-read/settings + SignalR `JoinTenantInbox` / business `JoinConversation` |
| `portal.messages` RBAC | `[RequiresPortalPermission]` on tenant messaging controller; hub uses `IPortalPermissionService` |
| Attachment safety | Tenant-prefixed blob keys, type/size limits, exists-before-send; missing blob → message still readable |
| PII | WhatsApp phone masked via `PiiMasker`; no message bodies in logs |
| Writes | REST-only; SignalR push-only |
| Portal UI | Staff-layout tokens (`--interactive`, Carbon icons); no hardcoded purple badge |
| New-message notifications | `ConversationMessageCreated` after commit + SignalR → prefs-aware stub dispatcher |
| Sprint 18 table drop | PreDeployment drops `messaging.messages` / `messaging.message_threads` if present |
