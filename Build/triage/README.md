# Build/triage — weekly AzDO triage helper

Operator documentation for the AzDO Pipeline at
[`Build/triage/pipelines/fetch.yml`](pipelines/fetch.yml).

## What it does

`PTVS-Triage-Fetch` runs in `devdiv/DevDiv` every Monday at 08:00 UTC. End to end:

1. **Query AzDO** for open work items under `DevDiv\Python and AI Tools\**`
   changed in the last 7 days (cap 25). Uses the pipeline's own
   `$(System.AccessToken)` for AzDO REST auth — no PAT needed on the AzDO side.
2. **Download + parse** any `PythonToolsDiagnostics_*.log` attachments per
   work item.
3. **Sanitize** all of it (PII redaction: emails, Windows user paths,
   machine names, phone numbers; secret detection: PATs, JWTs, AWS keys,
   private-key PEMs, etc.). Items that hit a secret pattern are dropped
   from every downstream output.
4. **Publish the sanitized data as an AzDO build artifact**
   (`sanitized-<buildId>`, internal-only audit trail) **and post a weekly
   summary issue** to `microsoft/PTVS`:
   - Title: `AzDO triage report YYYY-MM-DD to YYYY-MM-DD (N open items)`
   - Body: human-readable bullets per work item — title, AzDO link,
     created date, area subpath.
   - **NO sanitized content in the body**; just titles + links. The full
     sanitized JSON lives only in the AzDO build artifact.
   - Idempotent: searches by title, updates the body in place on rerun.

**Read-only by design.** The pipeline never writes to AzDO — no closes,
no labels, no comments. Engineers read the summary issue, open the AzDO
work items, and act manually inside AzDO.

## Why it lives in AzDO instead of GitHub Actions

DevDiv's tenant security gateway returns bare HTTP 401 to authenticated
requests from GitHub-hosted runner IPs, regardless of credential type
(verified empirically — captured `X-VSS-E2EID` in the rejected workflow
logs). The AzDO Pipeline runs *inside* DevDiv, where `$(System.AccessToken)`
gets through, then uses a fine-grained GitHub PAT (`GH_PAT`) for the only
outbound step that talks to GitHub: posting the summary issue.

| Side | Auth |
|---|---|
| AzDO REST (read work items, download attachments) | `$(System.AccessToken)` — automatic inside AzDO Pipelines in `devdiv/DevDiv` |
| GitHub from AzDO (issue posting) | Fine-grained GitHub PAT (`GH_PAT`) stored as AzDO variable group secret |

## Files

| Path | Role |
|---|---|
| `config.json` | Endpoints, area path, excluded states/tags, limits |
| `query-azdo.ps1` | WIQL query + `workitemsbatch` fetch. Runs in the AzDO pipeline. |
| `parse-diagnostics.ps1` | Downloads + parses `PythonToolsDiagnostics_*.log` attachments. |
| `sanitize.ps1` | PII / secret redaction. Full mode (one `wi-<id>.json` per candidate) and `-TitlesOnly` mode. |
| `post-report-issue.ps1` | Posts / updates the weekly summary issue in `microsoft/PTVS`. |
| `pipelines/fetch.yml` | The AzDO Pipeline YAML — the only thing that actually runs on a schedule. |
| `run-tests.ps1` | Runs `-SelfTest` on every script in this directory. |
| `tests/fixtures/candidates-sample.json` | Sample bare-array candidates for local `post-report-issue.ps1` dry-run. |
| `tests/fixtures/workitem-sample.json` | Sample AzDO `workitemsbatch` record used by `query-azdo.ps1` self-test. |

## One-time setup (operator)

### GitHub-side prep (~10 min, no admin needed)

1. **Mint a fine-grained GitHub PAT** for the AzDO Pipeline:
   - https://github.com/settings/personal-access-tokens/new
   - Resource owner: **microsoft**
   - Repositories: **only** `microsoft/PTVS`
   - Permissions:
     - **Issues**: Read and write *(post the weekly summary issue)*
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

### AzDO-side prep (~30 min, may need PCA help)

3. **Create variable group `PTVS-Triage-Bridge`** in `devdiv/DevDiv`:
   - Pipelines → Library → + Variable group
   - Add variable `GH_PAT` (mark as **secret**), paste the PAT from the previous step
   - Security: restrict pipeline-use to the upcoming `PTVS-Triage-Fetch`
     pipeline only (not "All pipelines").
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
   - A new build artifact `sanitized-<buildId>` on the AzDO run
   - A new issue with the weekly title in microsoft/PTVS, labeled
     `azdo-triage-report`, with bullet points for each work item

### Local testing (no AzDO needed)

The PowerShell scripts run on Windows or Linux with PowerShell 7:

```powershell
# Inline smoke tests on every script (no network).
pwsh -File Build/triage/run-tests.ps1

# Dry-run the issue-posting step against the bare-array fixture.
$env:GITHUB_TOKEN = '<your-fine-grained-PAT>'
./Build/triage/post-report-issue.ps1 `
  -CandidatesFile Build/triage/tests/fixtures/candidates-sample.json `
  -LookbackDays   7 `
  -RunUrl         'https://example.local' `
  -DryRun
```

## Operational notes

- **Manual re-runs**: Pipelines → PTVS-Triage-Fetch → Run pipeline. You
  can override the schedule branch (`main`) on the manual run to test
  changes from any branch in `microsoft/PTVS`.
- **Schedule + manual race**: if a manual run and the Monday cron fire on
  the same day, both target the same weekly issue title — `post-report-issue.ps1`
  searches for the existing issue and updates the body in place.
- **Failure recovery**: every step is idempotent. Re-run the pipeline and
  an existing weekly issue updates in place.
- **PR safety**: the pipeline has `trigger: none` AND `pr: none`. It will
  NOT run for PRs — important because it executes PS scripts from the
  checked-out branch with `$(System.AccessToken)` and the bridge PAT.
- **Auditing a past run**: anyone with access to the devdiv AzDO project
  can open the pipeline run and download the `sanitized-<buildId>` build
  artifact, which contains every `wi-<id>.json` and `diag-<id>.json` that
  was generated.
