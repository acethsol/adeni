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
