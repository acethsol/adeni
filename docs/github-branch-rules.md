# GitHub branch rules (Adeni)

## `dev` — CI gate before merge

PRs (and other updates) targeting **`dev`** should not merge until the **`ci-complete`** job from the [CI workflow](../.github/workflows/ci.yml) succeeds on the latest commit.

That job aggregates backend, database, npm audit, and frontend work so one green check is enough at the merge button.

### Apply or update the ruleset

Repo admins:

```powershell
pwsh scripts/github/apply-dev-ruleset.ps1
```

Source of truth: [`.github/rulesets/dev-require-ci.json`](../.github/rulesets/dev-require-ci.json).

### Manual setup (GitHub UI)

1. **Settings → Rules → Rulesets → New ruleset**
2. **Target:** Branches → include `dev` (or `refs/heads/dev`)
3. **Rules:** enable **Require status checks to pass**
   - Required check: **`ci-complete`**
   - **Require branches to be up to date before merging** (strict policy)
   - **Do not require status checks on creation** (so creating `dev` is not blocked)
4. Save and set enforcement to **Active**

### `main` (default branch)

**Ruleset A** already applies to `~DEFAULT_BRANCH` (`main`): PR + review, CodeQL, etc. It does not include `dev`.

To require CI on `main` as well, duplicate the status-check rule into Ruleset A or add `ci-complete` there in the UI.
