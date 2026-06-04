# Build/triage — weekly AzDO triage automation

Operator documentation for the workflow defined in
[`.github/workflows/azdo-triage.yml`](../../.github/workflows/azdo-triage.yml).
This README is the source of truth for day-to-day operation and the
phase 0 configuration that must be completed before flipping `dry_run` off.

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
| `tests/fixtures/` | Canned JSON fixtures for inline `-SelfTest` smoke tests. |
| `run-tests.ps1` | Runs `-SelfTest` on each script in this directory. Wired into the `triage-tests` job. |

> The judgment step (verdict per candidate) lives outside this directory in
> [`.github/workflows/azdo-triage-agent.md`](../../.github/workflows/azdo-triage-agent.md),
> an agentic workflow compiled by [`gh aw`](https://github.github.com/gh-aw/).
> Its companion `.lock.yml` is what GH Actions actually runs — see
> [Editing the triage prompt](#editing-the-triage-prompt) below.

## Secrets the workflow needs

| Secret | Purpose |
|---|---|
| `AZDO_TRIAGE_CLIENT_ID`, `AZDO_TRIAGE_TENANT_ID` | OIDC federation to a DevDiv Entra service principal. |
| `COPILOT_GITHUB_TOKEN` | PAT used by `gh-aw`'s `engine: copilot` to bill the GitHub Copilot CLI call. Must belong to an identity with an active GitHub Copilot Business / Enterprise entitlement. Validated by the agent job's first step; the workflow fails fast if absent. See [the gh-aw engines reference](https://github.github.com/gh-aw/reference/engines/#github-copilot-default). |
| `AZDO_PAT` *(fallback)* | PAT with `vso.work_write` if the SP/OIDC path isn't available. |

The workflow's writes to `microsoft/PTVS` issues (Job 1's weekly report and
Job 2's mirror issues) use the auto-injected `GITHUB_TOKEN`. The workflow's
top-level `permissions:` block declares `issues: write`, which is what scopes
that token. No separate PAT (`PTVS_BRIDGE_PAT`) is required.

The agent's GitHub MCP toolset (`issues` + `repos`, read-only) uses the
auto-injected, repo-scoped `GITHUB_TOKEN` and does NOT need a separate
PAT — gh-aw's compiled lockfile resolves
`GH_AW_GITHUB_MCP_SERVER_TOKEN || GH_AW_GITHUB_TOKEN || GITHUB_TOKEN`,
so the fallback covers us. The previous `PTVS_TRIAGE_MCP_READONLY_PAT`
secret was removed when the triage step was migrated from
`actions/ai-inference@v1` to `gh aw`.

The workflow also expects an Environment named `azdo-triage-approval` with
required reviewers configured.

## Phase 0 checklist (must complete before flipping `dry_run` to `false`)

Before the first run with `dry_run: false`, the following placeholders in
`config.json` MUST be replaced with the values observed on a real DevDiv
work item under our area path:

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

## Editing the triage prompt

The triage judgment lives in
[`.github/workflows/azdo-triage-agent.md`](../../.github/workflows/azdo-triage-agent.md),
authored as natural-language markdown with a YAML frontmatter block (engine,
tools, pre/post steps). GitHub Actions cannot execute the `.md` directly —
it runs a compiled `.lock.yml` sibling. After any edit to the `.md`, run:

```pwsh
# one-time, per workstation. The standard install command:
gh extension install github/gh-aw
# is blocked by the `github` org's SAML enforcement for non-org-member
# accounts (HTTP 403 from the releases API). Microsoft FTEs without
# membership in the github org should install manually from the prebuilt
# Windows binary instead:
$ext = Join-Path $env:LOCALAPPDATA "GitHub CLI\extensions\gh-aw"
New-Item -ItemType Directory -Path $ext -Force | Out-Null
$tag = (Invoke-WebRequest "https://github.com/github/gh-aw/releases/latest" `
        -MaximumRedirection 0 -SkipHttpErrorCheck).Headers.Location.Split('/')[-1]
Invoke-WebRequest "https://github.com/github/gh-aw/releases/download/$tag/windows-amd64.exe" `
    -OutFile (Join-Path $ext "gh-aw.exe") -UseBasicParsing
@{ name='gh-aw'; owner='github'; host='github.com'; tag=$tag; ispinned=$false;
   path=(Join-Path $ext 'gh-aw.exe'); isBinary=$true } | ConvertTo-Json |
    Out-File (Join-Path $ext 'manifest.yml') -Encoding ascii

# every time the .md changes:
gh aw compile .github/workflows/azdo-triage-agent.md
# commit BOTH the .md and the regenerated .lock.yml in the same commit
```

The `triage-tests` CI job verifies the lockfile is in sync with the source.

## Running locally

The scripts are PowerShell 7 and run on Windows or Linux runners.

```pwsh
# Run the inline smoke tests.
pwsh -File Build/triage/run-tests.ps1

# Dry-run the whole pipeline locally against a real AzDO token.
# For local runs, set GITHUB_TOKEN to a fine-grained PAT with
# issues:write + metadata:read on microsoft/PTVS. In GitHub Actions
# the auto-injected GITHUB_TOKEN is used instead — no PAT needed.
$env:AZDO_ACCESS_TOKEN = '...'
$env:GITHUB_TOKEN      = '...'
$env:DRY_RUN           = 'true'

./Build/triage/query-azdo.ps1            -OutFile $env:TEMP/candidates.json -LookbackDays 14
./Build/triage/sanitize.ps1 -TitlesOnly  -InFile $env:TEMP/candidates.json  -OutFile $env:TEMP/cands-titles.json
./Build/triage/post-report-issue.ps1     -CandidatesFile $env:TEMP/cands-titles.json -LookbackDays 14 -TriageWillRun $true -DryRun
```

See the [REST happy-path walkthrough](#rest-happy-path-walkthrough) below
for the curl-level sequence.

## REST happy-path walkthrough

For one-off manual reproduction of what the pipeline does, the curl-level
sequence per primary candidate is:

```bash
# 1. WIQL: enumerate the past 7 days under the area path
curl -s -H "Authorization: Bearer $AZDO_ACCESS_TOKEN" \
     -H 'Content-Type: application/json' \
     "$AZDO_BASE/$PROJECT/_apis/wit/wiql?api-version=7.0" \
     -d '{"query":"SELECT [System.Id] FROM WorkItems WHERE [System.AreaPath] UNDER \"DevDiv\\Python and AI Tools\" AND [System.CreatedDate] >= @Today - 7"}'

# 2. workitemsbatch: hydrate fields + tags
curl -s -H "Authorization: Bearer $AZDO_ACCESS_TOKEN" \
     -H 'Content-Type: application/json' \
     "$AZDO_BASE/$PROJECT/_apis/wit/workitemsbatch?api-version=7.0" \
     -d '{"ids":[12345,67890],"fields":["System.Title","System.State","System.Tags"]}'

# 3. comments: post the AI response or the DC duplicate template
curl -s -H "Authorization: Bearer $AZDO_ACCESS_TOKEN" \
     -H 'Content-Type: application/json' \
     "$AZDO_BASE/$PROJECT/_apis/wit/workItems/12345/comments?api-version=7.0-preview.3" \
     -d '{"text":"..."}'

# 4. PATCH: close as DC duplicate (test /rev guards concurrent edits)
curl -s -X PATCH -H "Authorization: Bearer $AZDO_ACCESS_TOKEN" \
     -H 'Content-Type: application/json-patch+json' \
     "$AZDO_BASE/$PROJECT/_apis/wit/workitems/12345?api-version=7.0" \
     -d '[{"op":"test","path":"/rev","value":430},
         {"op":"add","path":"/fields/System.State","value":"DC - Closed - Duplicated"},
         {"op":"add","path":"/fields/Microsoft.VSTS.Common.DuplicateFeedbackTicketId","value":"https://github.com/microsoft/PTVS/issues/8123"},
         {"op":"add","path":"/fields/System.Tags","value":"triaged-by-ai; from-azdo"}]'
```

## Reverting

Set `dry_run: true` from the **workflow_dispatch** UI, or disable the
workflow file entirely. Job 1 (the report) is low-risk and can be left on
while triage automation is paused.
