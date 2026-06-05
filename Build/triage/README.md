# Build/triage — weekly AzDO triage helper

Operator documentation for the AzDO Pipeline at
[`Build/triage/pipelines/fetch.yml`](pipelines/fetch.yml) (Phase 1, shipped)
and the GitHub workflow that will add AI analysis comments under the
weekly summary issue (Phase 2, deferred).

## What it does

### Phase 1 (shipped) — AzDO Pipeline `PTVS-Triage-Fetch` (devdiv/DevDiv)

Runs every Monday at 08:00 UTC. End to end:

1. **Cleanup**: delete draft releases tagged `triage-*` that are older than
   30 days (best-effort; doesn't fail the run on errors).
2. **Query AzDO** for open work items under `DevDiv\Python and AI Tools\**`
   changed in the last 7 days (cap 25). Uses the pipeline's own
   `$(System.AccessToken)` for AzDO REST auth — no PAT needed on the AzDO side.
3. **Download + parse** any `PythonToolsDiagnostics_*.log` attachments per
   work item.
4. **Sanitize** all of it (PII redaction: emails, Windows user paths,
   machine names, phone numbers; secret detection: PATs, JWTs, AWS keys,
   private-key PEMs, etc.).
5. **Build a slim manifest** (`bundle.json`) and **drop sanitization-aborted
   items** (anything that hit a secret pattern) from ALL three downstream
   outputs: public bullets, release assets, manifest.
6. **Create a DRAFT GitHub Release** in `microsoft/PTVS` tagged
   `triage-YYYY-MM-DD-<buildId>` and upload assets:
   - `bundle.json` — slim manifest listing work items with their asset names
   - `wi-<id>.json` per work item — full sanitized work item content
   - `diag-<id>.json` per item with diagnostics — parsed key fields
   Draft releases are **invisible to non-push-access users** (verified via
   GitHub REST docs: *"Only users with push access will receive listings for
   draft releases"*). So Microsoft-internal triage content stays out of public
   view even though `microsoft/PTVS` is a public repo.
7. **Post a weekly summary issue** to `microsoft/PTVS`:
   - Title: `AzDO triage report YYYY-MM-DD to YYYY-MM-DD (N open items)`
   - Body: human-readable bullets per work item + a hidden
     `<!--ptvs-triage-release: <tag>-->` marker pointing at the draft release
   - **NO sanitized content inline**; just a pointer
   - Idempotent: searches by title, updates in place on rerun
8. **Publish sanitized data as an AzDO build artifact** (audit trail,
   devdiv-internal).

### Phase 2 (planned) — GitHub workflow (microsoft/PTVS)

Triggered by `on: issues` for issues labeled `azdo-triage-report`. Will:
- Extract the `<tag>` from the hidden release-tag marker in the issue body
- Use the auto-injected `GITHUB_TOKEN` (`contents:read` is sufficient to
  download draft release assets) to fetch `bundle.json` and per-item
  `wi-<id>.json` / `diag-<id>.json` files
- Run an `actions/ai-inference@v1` matrix per work item: produce a short
  analysis of (a) likely root cause area — PTVS / pylance / debugpy /
  upstream CPython / customer config — and (b) suggested action items
- Post each analysis as a comment under the summary issue
- Delete the draft release once all comments are posted (or keep for 30d
  for audit; cleanup-on-next-run handles it either way)

**Read-only by design.** The AI never writes back to AzDO, never closes
work items, never tags / labels / files duplicates. Engineers read the
summary issue + AI comments and act manually inside AzDO.

## Why split between two systems

DevDiv's tenant security gateway returns bare HTTP 401 to authenticated
requests from GitHub-hosted runner IPs, regardless of credential type
(verified empirically — captured `X-VSS-E2EID` in the rejected workflow
logs). Workable auth paths:

| Side | Auth |
|---|---|
| AzDO REST (read work items, download attachments) | `$(System.AccessToken)` — automatic inside AzDO Pipelines in `devdiv/DevDiv` |
| GitHub from AzDO (issue + release writes) | Fine-grained GitHub PAT (`GH_PAT`) stored as AzDO variable group secret |
| GitHub from GH workflow (Phase 2) | Auto-injected `GITHUB_TOKEN` (`contents:read` reads draft assets; `issues:write` posts comments) |
| Cross-system handoff | A draft GitHub Release in `microsoft/PTVS`. Tag stored as hidden marker in the summary issue body. |

No private companion repo. No `repository_dispatch`. No data in the public
issue body.

## Files

| Path | Role |
|---|---|
| `config.json` | Endpoints, area path, excluded states/tags, limits, upstream tracker URLs |
| `query-azdo.ps1` | WIQL query + `workitemsbatch` fetch. Runs in the AzDO pipeline. |
| `sanitize.ps1` | PII / secret redaction. Full mode (one `wi-<id>.json` per candidate) and `-TitlesOnly` mode. |
| `parse-diagnostics.ps1` | Downloads + parses `PythonToolsDiagnostics_*.log` attachments. |
| `fetch-context.ps1` | GH-side per-run research context (recent commits, releases, fixed-in-next-version). For Phase 2. |
| `post-report-issue.ps1` | Posts / updates the weekly summary issue. `-ReleaseTag` adds a hidden marker pointing at the draft release. |
| `pipelines/fetch.yml` | The AzDO Pipeline YAML — the only thing that actually runs in Phase 1. |
| `run-tests.ps1` | Runs `-SelfTest` on every script in this directory. |
| `tests/fixtures/candidates-sample.json` | Sample bare-array candidates for local `post-report-issue.ps1` dry-run. |

## Phase 0 — One-time setup (operator)

### Phase 0a — GitHub-side prep (~10 min, no admin needed)

1. **Mint a fine-grained GitHub PAT** for the AzDO Pipeline:
   - https://github.com/settings/personal-access-tokens/new
   - Resource owner: **microsoft**
   - Repositories: **only** `microsoft/PTVS`
   - Permissions:
     - **Issues**: Read and write *(post the weekly summary issue)*
     - **Contents**: Read and write *(create draft releases + upload assets + delete stale drafts)*
     - **Metadata**: Read-only *(auto-granted)*
   - Expiration: **90 days** (set a calendar reminder for rotation)
   - Copy the token value (only shown once).
2. **Verify the issue label `azdo-triage-report` exists** (the pipeline
   will set this label when creating the issue, but the repo must allow
   the label to be created or it must already exist):
   ```powershell
   gh label create azdo-triage-report --color 0e8a16 --description 'Weekly AzDO triage report from PTVS-Triage-Fetch' --repo microsoft/PTVS
   # Idempotent — fails with "already exists" if so, safe to ignore.
   ```

### Phase 0b — AzDO-side prep (~30 min, may need PCA help)

3. **Create variable group `PTVS-Triage-Bridge`** in `devdiv/DevDiv`:
   - Pipelines → Library → + Variable group
   - Add variable `GH_PAT` (mark as **secret**), paste the PAT from Phase 0a
   - Security: restrict pipeline-use to the upcoming `PTVS-Triage-Fetch`
     pipeline only (not "All pipelines"). The minimum-bus-factor reviewers
     should be the only ones who can edit the variable group.
4. **Create the AzDO Pipeline** in `devdiv/DevDiv`:
   - Pipelines → New pipeline → GitHub (using existing service connection
     to `microsoft/PTVS`) → Existing Azure Pipelines YAML file → path:
     `/Build/triage/pipelines/fetch.yml`, branch: `main`
   - Name it `PTVS-Triage-Fetch`
   - Authorize use of variable group `PTVS-Triage-Bridge`
5. **Run the pipeline once manually** (it will fail — the Build Service
   identity doesn't have work-item permissions yet). The failure provisions
   the identity.
6. **Grant the Build Service identity read access to work items**
   (read-only — the pipeline never writes to AzDO):
   - https://dev.azure.com/devdiv/DevDiv/_settings/permissions
   - Search for identity: `[DevDiv]\DevDiv Build Service (devdiv)` or
     `Project Collection Build Service (devdiv)` (depends on pipeline scope)
   - Grant: **View work items in this node** at the project level
   - On area path `DevDiv\Python and AI Tools`: same permission (inherits to children)
7. **Re-run the pipeline manually** with `dryRun: true`. Verify it
   completes all steps and prints the would-be issue body in the
   `post-report-issue.ps1` step's log.
8. **Run without `dryRun`** for the first real summary issue. Expect to
   see:
   - A new draft release `triage-YYYY-MM-DD-<buildId>` in
     https://github.com/microsoft/PTVS/releases (visible only to push-access users)
   - A new issue with the weekly title in microsoft/PTVS, labeled
     `azdo-triage-report`, with bullets visible and a single hidden
     `<!--ptvs-triage-release: ...-->` line at the end of the body

### Phase 0c — Local testing (no AzDO needed)

The PowerShell scripts run on Windows or Linux with PowerShell 7:

```powershell
# Inline smoke tests on every script (no network).
pwsh -File Build/triage/run-tests.ps1

# Dry-run the issue-posting step against the bare-array fixture.
$env:GITHUB_TOKEN = '<your-fine-grained-PAT-from-Phase-0a>'
./Build/triage/post-report-issue.ps1 `
  -CandidatesFile Build/triage/tests/fixtures/candidates-sample.json `
  -ReleaseTag     'triage-2026-06-05-local' `
  -LookbackDays   7 `
  -RunUrl         'https://example.local' `
  -DryRun
```

## Operational notes

- **Manual re-runs**: Pipelines → PTVS-Triage-Fetch → Run pipeline. You
  can override the schedule branch (`main`) on the manual run to test
  changes from any branch in `microsoft/PTVS`.
- **Schedule + manual race**: if a manual run and the Monday cron fire on
  the same day, both create separate draft releases (tags differ by build
  id), but both target the same weekly issue title — `post-report-issue.ps1`
  searches for the existing issue and updates in place. The body will end
  up pointing at the later run's release; the earlier release becomes
  orphaned and gets cleaned up at the 30-day mark.
- **Failure recovery**: every step is idempotent except the draft-release
  creation (each run creates a NEW tag — guaranteed unique via build id).
  Re-run the pipeline and an existing weekly issue updates in place with
  the new release tag pointer.
- **PR safety**: the pipeline has `trigger: none` AND `pr: none`. It will
  NOT run for PRs — important because it executes PS scripts from the
  checked-out branch with `$(System.AccessToken)` and the bridge PAT.
- **Auditing a past run**: anyone with push access on microsoft/PTVS can
  open the Releases page, filter by the `triage-` tag prefix, and download
  the original `bundle.json` + `wi-<id>.json` + `diag-<id>.json` files.

## History

- **2026-06** (in progress) — Architecture refined twice:
  - First simplified scope: dropped duplicate detection, AzDO writes, GitHub
    mirror issues, and the gh-aw agentic step. Initial design embedded a
    base64-encoded slim bundle in the public issue body (still visible to
    public viewers, even if hidden from rendering).
  - Then moved the bundle out of the issue body entirely. Sanitized data now
    lives in draft GitHub Releases (access-controlled to push-access users).
    Issue body is just bullets + a release-tag pointer.
- **2026-05** — Initial implementation with full triage pipeline (`PR #8523`).
  Never ran successfully end-to-end because DevDiv blocks GitHub-hosted
  runner IPs from authenticating to AzDO.
