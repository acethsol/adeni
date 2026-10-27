# @adeni/ui

Shared Angular UI for Adeni staff apps (`portal`, `admin`). Framework-agnostic contracts stay in `@adeni/shared`; HTTP in `@adeni/api-client`.

## Usage

```typescript
import { PortalPageComponent } from "@adeni/ui";
```

Component styles (cards, buttons, form fields):

```scss
@use "staff-layout" as *;
```

Requires `stylePreprocessorOptions.includePaths` → `../../packages/ui/src/styles` in the app `angular.json`.

App-specific widgets remain under each app (e.g. portal `pending-bookings-bell`).

Document titles (browser tab):

```typescript
import { provideAdeniDocumentTitle } from "@adeni/ui";

// app.config.ts — with `title` on router routes
...provideAdeniDocumentTitle("Adeni Business");
// Portal: after profile load, AdeniDocumentTitleStrategy.setAppTitle(businessName)
```

## Confirmation dialogs (`AdeniConfirmService`)

Significant, state-changing actions must ask before calling the API. `<adeni-confirm-host />` is already mounted in the portal, admin and discover shells.

```typescript
private readonly confirmDialog = inject(AdeniConfirmService);

// Preset copy: common.confirm.<kind>.{title,message,action} (EN + FR in @adeni/shared)
if (!(await this.confirmDialog.confirmKind("delete", { subject: location.name }))) return;

// Fully custom copy
const ok = await this.confirmDialog.confirm({
  title, message, confirmLabel, cancelLabel,
  variant: "danger",                 // "primary" (default) | "danger"
  requireAcknowledgement: true,      // optional "I understand" checkbox gates the confirm button
  acknowledgementLabel,              // optional override of common.confirm.understand
});
```

`confirmKind` kinds: `save submit send create activate approve grant reactivate` (primary) and `delete remove deactivate reject deny revoke cancel` (danger by default). Options: `subject` (fills `{subject}` in the message; pass a translated noun such as `tr("common.confirm.subjects.profile")` or a record name), `title`, `message`, `confirmLabel`, `variant`, `requireAcknowledgement`. Adding a kind = add it to `AdeniConfirmKind` and add EN + FR `common.confirm.<kind>.*` keys (`confirm-copy.test.ts` guards this).

Put the confirm **inside the handler, before** the busy flag / API call, after local validation (so validation errors are not preceded by a modal).

| Action | Confirm? |
|--------|----------|
| Save / create / submit / send on a main form (profile, settings, services, staff, locations, pricing, markets) | Yes — `save` / `create` / `submit` / `send` |
| Delete, remove, revoke, deactivate, reject, deny, cancel, refund | Yes — danger variant |
| Approve, grant, activate, go live, mark paid, complete booking | Yes — primary variant |
| Irreversible / data-erasing (e.g. privacy erasure) | Yes — danger + `requireAcknowledgement` |
| Refresh, search, filter, sort, pagination, tab / navigation, cancel / close, export menu, theme toggle, language switch | No |
| Autosave, layout customisation (dashboard widgets), file-picker uploads, chat send, high-frequency ops (check-in, queue start/complete) | No |
| Modal that already collects a required reason (admin reject business) | No extra confirm — the reason modal is the confirmation |
