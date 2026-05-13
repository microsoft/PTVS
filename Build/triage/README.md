# Build/triage — weekly AzDO triage automation

Operator documentation for the workflow defined in
[`.github/workflows/azdo-triage.yml`](../../.github/workflows/azdo-triage.yml).
Full design is in [`plan.md`](../../plan.md) at the repo root — that file is
the source of truth for behavioral decisions; this README is the day-to-day
operator's reference.

## What it does

Every Monday at 08:00 UTC the workflow:

1. **Job 1 — Weekly report (always runs).** Queries Azure DevOps for open work
   items under `DevDiv\Python and AI Tools\**` created in the last 7 days and
   files a single summary issue in `microsoft/PTVS` titled
   `AzDO triage report YYYY-MM-DD to YYYY-MM-DD (<N> open items)`. The only
   write is this read-only-style summary.
2. **Job 2 — Triage pipeline (conditional on `run_triage`).** Runs as three
   GitHub-Actions jobs (`prepare` → `triage` → `apply`). The `apply` step
   needs a human reviewer to approve the `azdo-triage-approval` environment
   before any AzDO/GitHub writes happen.

## Files

| Path | Role |
|---|---|
| `config.json` | Endpoints, area path, label whitelist, confidence thresholds, DC field/state names. **Phase 0 must overwrite `azdo.duplicateFieldName` and verify `azdo.states.*`.** |
| `query-azdo.ps1` | WIQL query + `workitemsbatch` fetch. Used by Job 1 and Job 2's `prepare`. |
| `sanitize.ps1` | PII / secret redaction. `-TitlesOnly` is the lightweight Job 1 path; full mode is the Job 2 path. |
| `post-report-issue.ps1` | Job 1 writer. Idempotent (search-by-title before create). |
| `fetch-context.ps1` | Per-run research context (commits, releases, fixed-in-next-version, area-path history). |
| `parse-diagnostics.ps1` | Downloads `PythonToolsDiagnostics_*.log` attachments and extracts the structured header. |
| `cluster.ps1` | Within-batch dedup via offline Jaccard on title+body tokens. Outputs `clusters.json` and `primaries.json`. |
| `post-azdo.ps1` | Helpers (dot-sourced by `apply-outcomes.ps1`): comment, close-as-duplicate, close-as-answered. Concurrency-safe (uses `test /rev`). |
| `mirror-to-github.ps1` | Helpers (dot-sourced by `apply-outcomes.ps1`): search-then-create mirror issue on `microsoft/PTVS`. |
| `apply-outcomes.ps1` | Reads verdicts + cluster map and dispatches the per-candidate actions. Honors `$env:DRY_RUN`. |
| `prompts/triage.prompt.yml` | Main AI triage prompt consumed by `actions/ai-inference`. |
| `tests/fixtures/` | Canned JSON fixtures for inline `-SelfTest` smoke tests. |
| `run-tests.ps1` | Runs `-SelfTest` on each script in this directory. Wired into the `triage-tests` job. |

## Secrets the workflow needs

| Secret | Purpose |
|---|---|
| `AZDO_TRIAGE_CLIENT_ID`, `AZDO_TRIAGE_TENANT_ID` | OIDC federation to a DevDiv Entra service principal. |
| `PTVS_BRIDGE_PAT` | Fine-grained PAT (or App token) with `issues:write` + `metadata:read` on `microsoft/PTVS`. |
| `PTVS_TRIAGE_MCP_READONLY_PAT` | Separate read-only token for `actions/ai-inference`'s GitHub MCP server. |
| `AZDO_PAT` *(fallback)* | PAT with `vso.work_write` if the SP/OIDC path isn't available. |

The workflow also expects an Environment named `azdo-triage-approval` with
required reviewers configured (plan §9.2).

## Phase 0 checklist (must complete before flipping `dry_run` to `false`)

Per plan §6.3 / §11 Q11, the following placeholders in `config.json` MUST be
replaced with the values observed on a real DevDiv work item under our area
path:

1. `azdo.duplicateFieldName` — the REST field name behind the
   "Duplicate Feedback Ticket ID" field on the Overview tab. Likely
   `Microsoft.VSTS.Common.DuplicateFeedbackTicketId` but verify with
   `GET .../wit/workitems/{id}?$expand=all` and inspect `fields`.
   `Close-AzdoAsDuplicate` will refuse to PATCH while the placeholder
   string is still set.
2. `azdo.states.duplicate`, `azdo.states.answered`, `azdo.states.fixed` —
   the exact DC close-state strings available on the work item type. Defaults
   are best public guesses (`DC - Closed - Duplicated`, `DC - Closed - Other`,
   `DC - Closed - Fixed`).
3. Verify that `from-azdo` and `azdo-triage-report` labels exist on
   `microsoft/PTVS` (create them once before the first non-dry-run).

## Running locally

The scripts are PowerShell 7 and run on Windows or Linux runners.

```pwsh
# Run the inline smoke tests.
pwsh -File Build/triage/run-tests.ps1

# Dry-run the whole pipeline locally against a real AzDO token.
$env:AZDO_ACCESS_TOKEN = '...'
$env:PTVS_BRIDGE_PAT   = '...'
$env:DRY_RUN           = 'true'

./Build/triage/query-azdo.ps1            -OutFile $env:TEMP/candidates.json -LookbackDays 14
./Build/triage/sanitize.ps1 -TitlesOnly  -InFile $env:TEMP/candidates.json  -OutFile $env:TEMP/cands-titles.json
./Build/triage/post-report-issue.ps1     -CandidatesFile $env:TEMP/cands-titles.json -LookbackDays 14 -TriageWillRun $true -DryRun
```

See `plan.md` §13 for the curl-level "happy path" sequence.

## Reverting

Set `dry_run: true` from the **workflow_dispatch** UI, or disable the
workflow file entirely. Job 1 (the report) is low-risk and can be left on
while triage automation is paused.
