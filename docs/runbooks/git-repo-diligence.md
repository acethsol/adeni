# Git repository due diligence (Adeni)

Repo: **https://github.com/acethsol/adeni**

Use this checklist before QA deploy and before opening the repo to more contributors. Adjust to your org standards (Aceth).

---

## 1. Branch and release model

**Current state (verify on GitHub):**

- Default branch: **`main`** (per `origin/HEAD`)
- Active integration branch: **`dev`** (Sprint work may land here first)
- CI runs on: **`main`** push and PRs to **`main`** only ([ci.yml](../../.github/workflows/ci.yml))

**Recommended policy:**

| Rule | Why |
|------|-----|
| **`main` = deployable** | QA/prod build from `main` (or tags on `main`) |
| **`dev` → `main` via PR** | No direct pushes to `main` |
| **Short-lived feature branches** | `feature/…`, `fix/…` → PR to `dev` or `main` per team habit |
| **Tag QA releases** | e.g. `qa-2026-09-21` or `v0.17.0-qa` for traceability |

**Action:** If CI should gate `dev` too, add `dev` to `.github/workflows/ci.yml` `branches:` list.

---

## 2. GitHub settings (Settings → General / Branches)

### 2.1 Branch protection — `main`

Enable for **`main`** (and optionally **`dev`**):

- [ ] Require a pull request before merging
- [ ] Require approvals: **1** (2 for prod later)
- [ ] Require status checks to pass: **`build-and-test`** (CI job name)
- [ ] Require branches to be up to date before merge
- [ ] Do not allow bypassing the above for admins (or limit to release managers)
- [ ] Restrict who can push (optional: no direct push, PR only)

### 2.2 Branch protection — `dev` (optional but useful)

- [ ] Require PR + CI green (lighter than `main`)
- [ ] Allow maintainers to merge without review for hotfixes (team choice)

### 2.3 Repository security

- [ ] **Private** repo until public launch (confirm visibility)
- [ ] **Dependabot** enabled — [dependabot.yml](../../.github/dependabot.yml) exists; confirm alerts in GitHub Security
- [ ] **Secret scanning** + **push protection** (GitHub Advanced Security if licensed)
- [ ] **Code scanning** (CodeQL) — add workflow when ready
- [ ] Disable **wiki** if unused; use `docs/` as source of truth

---

## 3. Secrets and credentials

| Do | Don't |
|----|--------|
| Store QA/prod secrets in **Azure Key Vault** + ACA secret refs | Commit `.env.local`, Paystack keys, Auth0 client secrets |
| Use **GitHub Environments** (`qa`, `production`) for deploy secrets | Share production connection strings in chat |
| Rotate Auth0 client secrets on leak | Reuse dev passwords on QA Postgres |

**Audit locally:**

```powershell
git log -p --all -S "password" -- "*.json" "*.env*"
```

Confirm `.gitignore` covers: `.env.local`, `apps/web/.env.local`, user secrets, `.data/`.

---

## 4. CI quality gates (already in repo)

| Gate | Location |
|------|----------|
| Build + test | `dotnet test Adeni.slnx -c Release` with Postgres + Redis services |
| Vulnerable packages | `dotnet list package --vulnerable` |

**Enhancements to consider:**

- [ ] `npm ci` + typecheck/lint for `apps/web` on PR
- [ ] Fail CI on vulnerable packages (currently report-only unless you add a step)
- [ ] Optional: Playwright smoke against docker-compose (future)

---

## 5. CODEOWNERS and review

Add [`.github/CODEOWNERS`](../../.github/CODEOWNERS) when team grows, e.g.:

```text
* @acethsol/core-team
/src/Adeni.Api/ @acethsol/backend
/apps/web/ @acethsol/frontend
/docs/ @acethsol/core-team
```

Requires branch protection “Require review from Code Owners” for critical paths.

---

## 6. Issues, PRs, and traceability

- [ ] PR template: summary, test plan, link spec (`docs/specs/…`) for non-trivial features
- [ ] Link PRs to sprint / Jira / Confluence
- [ ] Squash merge vs merge commit — pick one convention for `main` (squash keeps history readable)

---

## 7. Compliance hooks (SOC 2 alignment)

From [prd-v1.1-body.md](../prd-v1.1-body.md):

- [ ] **SOC2-04** — HTTPS only on QA (ACA ingress + HSTS)
- [ ] **SOC2-05** — No secrets in repo; Key Vault for Staging+
- [ ] **SOC2-06** — Admin MFA via Auth0 on QA
- [ ] Admin mutations audited — already in API; verify QA admin actions write audit logs

---

## 8. Due diligence walkthrough (30 minutes)

1. GitHub → **Settings → Branches** — protection on `main`?
2. **Actions** — last `main` CI green?
3. **Security** — Dependabot alerts count?
4. **Collaborators** — least privilege (no unnecessary admins)
5. Local: `git branch -a` — stale `cursor/*` remote branches — delete or archive policy?
6. Confirm **`dev`** is ahead of **`main`** — plan merge before QA deploy

---

## 9. Suggested order of operations

1. Enable branch protection + CI required on **`main`**
2. Merge **`dev` → `main`** when ready for QA baseline
3. Add Dockerfiles + deploy workflow (see [qa-deploy.md](./qa-deploy.md))
4. Create GitHub Environment **`qa`** with secrets / OIDC to Azure (no long-lived Azure SP passwords in GitHub if avoidable)

---

## 10. Azure ↔ GitHub (Container Apps)

- **OIDC federated credential** — GitHub Actions assumes Azure role to push ACR and update ACA (preferred over client secret)
- **Environment protection** — require reviewer before deploy to `qa`; stricter for `production` later
- Store `AZURE_*`, ACR name, resource group as **environment secrets**, not repository secrets, when repos multiply

See [qa-deploy.md §9](./qa-deploy.md) for deploy flow.
