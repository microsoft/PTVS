# Build/triage — weekly AzDO triage helper (read + AI draft)

Operator documentation for the workflow defined in
[`.github/workflows/triage-draft.yml`](../../.github/workflows/triage-draft.yml)
and the companion AzDO Pipeline at `Build/triage/pipelines/fetch.yml` (added
in Phase 2).

## What it does

Once per week (manually for now; cron later) the system:

1. **AzDO Pipeline (devdiv/DevDiv)** — Fetches open work items under
   `DevDiv\Python and AI Tools\**` created in the lookback window, sanitizes
   them (PII / secret redaction), downloads + parses any
   `PythonToolsDiagnostics_*.log` attachments, and pushes a single
   `input.json` to the private companion repo `microsoft/PTVS-triage-data`
   under `runs/<correlation_id>/`.
2. **GitHub workflow (microsoft/PTVS)** — Checks out `input.json` from the
   companion repo at the exact `data_sha`, asks GitHub Models (`actions/ai-inference`)
   to draft a suggested customer response per work item (matrix, max 3 parallel),
   and posts a single weekly summary issue listing the items with the drafts
   inlined under each one.

**Read-only by design.** The AI never:

- replies to the AzDO work item
- closes or changes the state of any work item
- changes labels / tags / area paths
- mirrors items into GitHub issues
- finds or marks duplicates

The drafts are review-only — a human reads the weekly summary issue and
manually copies good drafts into AzDO comments. This is the explicit
2026-06 simplification (removed `apply-outcomes.ps1`, `post-azdo.ps1`,
`mirror-to-github.ps1`, `cluster.ps1`).

## Why split between two systems

DevDiv's tenant security gateway returns bare HTTP 401 to authenticated
requests from GitHub-hosted runner IPs, regardless of whether the credential
is a PAT or an Entra OIDC bearer token (verified empirically — `X-VSS-E2EID`
captured in the rejected workflow logs). The only paths that work end-to-end
without admin onboarding are:

| Side | Auth |
|---|---|
| AzDO REST (read work items, download attachments) | `$(System.AccessToken)` natively available inside AzDO Pipelines in `devdiv/DevDiv` |
| GitHub API (post weekly issue, run AI) | Auto-injected `GITHUB_TOKEN` natively available in GH Actions |
| Cross-system handoff | Private git companion repo `microsoft/PTVS-triage-data`, one fine-grained PAT per direction |

See [`Build/triage/handoff-schema.md`](handoff-schema.md) for the JSON
schemas used for the handoff.

## Files

| Path | Role |
|---|---|
| `config.json` | Endpoints, area path, excluded states/tags, limits, upstream tracker URLs |
| `handoff-schema.md` | JSON contract for `input.json` and `drafts.json` |
| `query-azdo.ps1` | WIQL query + `workitemsbatch` fetch. Runs in the AzDO pipeline. |
| `sanitize.ps1` | PII / secret redaction. Full mode (one `wi-<id>.json` per candidate) and `-TitlesOnly` mode. |
| `parse-diagnostics.ps1` | Downloads + parses `PythonToolsDiagnostics_*.log` attachments. |
| `fetch-context.ps1` | GH-side per-run research context (recent commits, releases, fixed-in-next-version). |
| `post-report-issue.ps1` | Posts / updates the weekly summary issue in `microsoft/PTVS`. Optional `-DraftsFile` inlines drafts under each bullet. |
| `run-tests.ps1` | Runs `-SelfTest` on every script in this directory. Wired into the `triage-tests` job. |
| `tests/fixtures/input-sample.json` | Sample `input.json` for local testing without the companion repo. |
| `pipelines/fetch.yml` *(Phase 2)* | AzDO Pipeline YAML — fetch + sanitize + push to companion repo. Not yet committed. |

The AI prompt lives in
[`.github/prompts/triage-draft.prompt.yml`](../../.github/prompts/triage-draft.prompt.yml)
as a structured GitHub Models `.prompt.yml` (system + user messages, model
choice, temperature). Bump `prompt_version` in `triage-draft.yml` if you
materially change it so older drafts are auditable.

## Secrets and access required

### On the GitHub side (microsoft/PTVS)

| Secret | Purpose |
|---|---|
| `PTVS_TRIAGE_DATA_PAT` | Fine-grained PAT for checking out `microsoft/PTVS-triage-data` at a specific SHA (private repo, so the auto-injected `GITHUB_TOKEN` doesn't suffice). Phase 1 also uses this; Phase 3 will switch to a GitHub App installation token. |
| `GITHUB_TOKEN` *(auto-injected)* | Used for `actions/ai-inference` (workflow declares `models: read`) and for posting the weekly summary issue (workflow declares `issues: write`). |

### On the AzDO side (devdiv/DevDiv, Phase 2)

| Setting | Purpose |
|---|---|
| `$(System.AccessToken)` | Native bearer token for the pipeline's Build Service identity. Has WIT scopes — but the Build Service identity must be granted `View work items` + `Edit work items` on the area path `DevDiv\Python and AI Tools` before it actually authorizes anything (see Phase 0b below). |
| Variable group `PTVS-Triage-Bridge` | Holds `PTVS_BRIDGE_PAT` (fine-grained GitHub PAT scoped to `microsoft/PTVS-triage-data` with `contents:write`, and `microsoft/PTVS` with `repository_dispatch:write` for Phase 3). |

## Phase 0 — One-time setup

### Phase 0a — GitHub setup (~10 min, no admin required)

1. **Create private repo `microsoft/PTVS-triage-data`**:
   - Visibility: **private**
   - Disable: issues, wiki, discussions, projects, packages
   - Access: invite ONLY @StellaHuang95 + 1 backup reviewer (avoid inheriting microsoft/PTVS's broader access)
2. **Generate a fine-grained PAT** at https://github.com/settings/personal-access-tokens/new:
   - Resource owner: **microsoft**
   - Repositories: select `PTVS-triage-data` and `PTVS`
   - Permissions on `PTVS-triage-data`: **Contents: Read and write**
   - Permissions on `PTVS`: **Issues: Read** (the workflow's own `GITHUB_TOKEN` handles writes), **Metadata: Read**, **Contents: Read**, and *(Phase 3)* **Repository dispatch: Write**
   - Expiration: **90 days** (set a calendar reminder for rotation)
3. **Set the GitHub secret**:
   ```powershell
   gh secret set PTVS_TRIAGE_DATA_PAT --repo microsoft/PTVS
   ```

### Phase 0b — AzDO setup (~30 min, may need admin help) — Phase 2 only

These steps are only needed when adding the AzDO Pipeline (`pipelines/fetch.yml`)
in Phase 2. Skip for Phase 1 fixtures testing.

4. **Create the AzDO Pipeline `PTVS-Triage-Fetch`** in `devdiv/DevDiv` pointing at `Build/triage/pipelines/fetch.yml` on the `main` branch of `microsoft/PTVS`.
5. **Run it once** so the Build Service identity is provisioned (it'll fail — that's expected).
6. **Grant the Build Service identity work-item permissions** (read-only — the AzDO Pipeline only fetches; it never writes):
   - https://dev.azure.com/devdiv/DevDiv/_settings/permissions → search `DevDiv Build Service (devdiv)`
   - Grant: **View work items in this node** at the project level
   - On area path `DevDiv\Python and AI Tools`: same permission (inherited to children)
7. **Create variable group `PTVS-Triage-Bridge`** with `PTVS_BRIDGE_PAT` (marked secret); restrict pipeline access to the `PTVS-Triage-Fetch` pipeline only.

### Phase 0c — Phase 1 testing without companion repo (0 min)

The new GH workflow's `fixtures_path` input bypasses the companion-repo
checkout entirely. Run a smoke test now:

```powershell
gh workflow run triage-draft.yml --repo microsoft/PTVS `
  --ref fix/triage-reusable-workflow-permissions `
  -f correlation_id=local-fixture-001 `
  -f fixtures_path=Build/triage/tests/fixtures/input-sample.json `
  -f lookback_days=7 `
  -f dry_run=true
```

This exercises the matrix + AI inference + issue-formatting end-to-end against
the one-candidate sample fixture under `Build/triage/tests/fixtures/`. With
`-f dry_run=true` the workflow logs the issue body instead of posting.

## Running locally

The PowerShell scripts are PowerShell 7 and run on Windows or Linux:

```pwsh
# Run inline smoke tests on every script.
pwsh -File Build/triage/run-tests.ps1

# Dry-run the report-posting step against the fixtures (no AzDO calls, no AI).
# candidates-sample.json is the bare array shape post-report-issue.ps1 expects;
# drafts-sample.json is shaped like the workflow's aggregate step produces.
$env:GITHUB_TOKEN = '...'  # fine-grained PAT with issues:write on microsoft/PTVS
./Build/triage/post-report-issue.ps1 `
  -CandidatesFile Build/triage/tests/fixtures/candidates-sample.json `
  -DraftsFile     Build/triage/tests/fixtures/drafts-sample.json `
  -LookbackDays   7 `
  -RunUrl         'https://example.local' `
  -DryRun
```

## Reverting

The workflow is `workflow_dispatch`-triggered only — there's no schedule to
disable. To "stop" it, simply don't trigger it (and don't run the AzDO
Pipeline that populates the companion repo).

To remove permanently: delete `.github/workflows/triage-draft.yml`,
`.github/prompts/triage-draft.prompt.yml`, and `Build/triage/pipelines/`.
The supporting PS scripts (`query-azdo.ps1`, `sanitize.ps1`,
`parse-diagnostics.ps1`, `fetch-context.ps1`, `post-report-issue.ps1`) have
no other consumers and can also be deleted.

## History

- **2026-06** — Simplified scope: dropped duplicate detection, AzDO writes,
  GitHub mirror issues, and the gh-aw agentic step. Replaced with a simple
  `actions/ai-inference` matrix that drafts a suggested response per work
  item. Reason: the destructive paths added significant complexity (verdict
  schema, confidence thresholds, environment approvals, state machine
  config) that wasn't earning its keep for a team in maintenance mode.
- **2026-05** — Initial implementation with full triage pipeline (`PR #8523`).
  Never ran successfully end-to-end because DevDiv blocks GitHub-hosted
  runner IPs from authenticating to AzDO.
