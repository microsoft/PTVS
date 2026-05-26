# Weekly Azure DevOps Issue Triage Automation — Implementation Plan

## 1. Goal

Automate the weekly triage of customer-reported issues filed under the
Azure DevOps area path `DevDiv\Python and AI Tools\**` (the internal
DevDiv project on `https://dev.azure.com/devdiv`). PTVS is in
maintenance mode and customer traffic on that area path is low; the
goal is to have the agent handle each item without inventing a
heavyweight workflow around it.

The workflow has two **logical** jobs:

### Job 1 — Weekly report (always runs)

Queries AzDO for all open work items under the area paths that were
created in the last 7 days and posts a single summary issue to
`microsoft/PTVS` titled `AzDO triage report YYYY-MM-DD to YYYY-MM-DD
(<N> open items)`. Body is a bullet list of `[AzDO #<id>](<url>) —
<sanitized title> — created <date> — area: <subpath>` lines. Filed
every run regardless of whether the triage job runs — it's the team's
always-on visibility into what came in this week, useful even when
the heavier triage pipeline is disabled or broken.

### Job 2 — Triage pipeline (enabled by default, opt-out on manual dispatch)

Per-candidate research (recent PTVS commits, releases,
`fixed in next version` candidates, parsed diagnostics log, AzDO
area-path history), within-batch dedup, AI triage with one of two
outcomes per candidate:

1. **Respond on AzDO and close.** The AI composed a tailored answer
   the customer can act on now — either because the issue is already
   fixed in a released or upcoming version, or because we can apply
   (and adapt) the resolution from a duplicate report. The full
   response goes into the AzDO comment so the customer doesn't have
   to follow a link.

2. **Mirror to `microsoft/PTVS` and close AzDO as a Developer
   Community duplicate.** For everything else — including
   `needs_info` reports — create a public GitHub issue with the
   sanitized content, apply the appropriate labels (`bug`,
   `needs repro`, `from-azdo`, and `waiting for response` when
   applicable so the existing
   [auto-label.yml](.github/workflows/auto-label.yml) and
   [stale.yml](.github/workflows/stale.yml) workflows take over the
   follow-up automatically), assign to a maintainer
   (`StellaHuang95` by default), and close the AzDO work item using
   the Developer Community duplicate workflow: set state to
   **`DC - Closed - Duplicated`**, populate the
   **`Duplicate Feedback Ticket ID`** field on the Overview tab
   with the new GitHub issue URL, and post the canned DC duplicate
   template as a comment so the customer sees the standard "we've
   transferred your votes; please follow along on the linked
   ticket" message they're used to from other VS feedback.

Job 2 is enabled by default on scheduled runs. On manual
`workflow_dispatch` invocations, the user sees a checkbox
(`run_triage`, default `true`) — unchecking it makes the workflow
run only Job 1. This is the maintainer's escape hatch for weeks
when they want the summary but don't want any AzDO/GitHub writes
to happen.

The pipeline runs on a weekly schedule (target: Monday 08:00 UTC).

> **What the bridge does NOT do automatically (by design, given low
> traffic):** assign mirrored issues to Copilot cloud agent for a
> draft PR. Maintainers do that themselves via GitHub's "assign to
> Copilot" UI or the [Azure Boards Copilot
> integration](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/integrate-cloud-agent-with-azure-boards)
> (§6.4) when a report justifies the Copilot-premium-request cost.

---

## 2. Feasibility summary

| Requirement | Feasible? | Notes |
|---|---|---|
| Weekly schedule | ✅ | GitHub Actions `schedule: cron`. |
| Query AzDO work items by area path | ✅ | `POST /wit/wiql?api-version=7.1` with `[System.AreaPath] UNDER 'DevDiv\Python and AI Tools'` and `[System.CreatedDate] >= @Today - 7`. |
| Read work item fields, comments, attachments | ✅ | `GET /wit/workitems/{id}?$expand=all`, `GET /wit/workItems/{id}/comments`, `GET /wit/attachments/{guid}`. |
| AI-driven triage / draft response | ✅ | `actions/ai-inference@v1` + GitHub Models + GitHub MCP `issues` toolset (so the AI can fetch comments on a duplicate). See §5. |
| AI proposes code fix | ✅ (manual) | Copilot cloud agent. Maintainer-initiated; NOT automated by the bridge. |
| Create GitHub issue in `microsoft/PTVS` | ✅ | GitHub REST API with a fine-grained PAT or a GitHub App. |
| Close AzDO work item using the Developer Community duplicate flow | ✅ | `PATCH /wit/workitems/{id}` (set `System.State` → `DC - Closed - Duplicated`, populate `Duplicate Feedback Ticket ID` field) + `POST /wit/workItems/{id}/comments` with the canned DC duplicate template. See §6.3 Step 6. |
| Cross-tenant auth (GitHub Actions → DevDiv AzDO) | ✅ | Workload identity federation (OIDC) from GitHub → Microsoft Entra service principal → AzDO. PAT is a fallback. |
| Fully autonomous (no human in the loop) | ⚠️ **Not recommended for v1** | Risk of incorrect closures and public PII leaks. v1 requires human approval before destructive actions. |

**Conclusion:** Feasible end-to-end. The single biggest risk is
inadvertent disclosure of customer PII when mirroring an internal
AzDO work item to public `microsoft/PTVS`. Mitigated by sanitization
+ human approval gate (see §9).

---

## 3. Existing context in this repo (what we already have)

PTVS already has issue-automation precedent in [.github/workflows](.github/workflows):

- [auto-label.yml](.github/workflows/auto-label.yml) — toggles
  `waiting for response` / `user responded` labels on comments.
- [issues.yml](.github/workflows/issues.yml) — round-robin assignment
  to `bschnurr`, `heejaechang`, `StellaHuang95`, `rchiodo`; labels new
  issues `needs repro`.
- [stale.yml](.github/workflows/stale.yml) — auto-closes issues that
  have been `waiting for response` for 30 days.

The product itself is the Python workload for Visual Studio
(C# / C++ extension), built with Azure Pipelines using the 1ES
Pipeline Template. See [README.md](README.md) and
[azure-pipelines.yml](azure-pipelines.yml). Public bug tracker is
`github.com/microsoft/PTVS`; the internal tracker uses area paths
under `DevDiv\Python and AI Tools\**`.

The plan re-uses these conventions:

- New workflow placed under [.github/workflows/](.github/workflows/).
- Labels and assignees consistent with `issues.yml`; default
  `StellaHuang95` for the bridge.
- The mirrored issue uses the existing
  [bug-report.md](.github/ISSUE_TEMPLATE/bug-report.md) shape.

---

## 4. High-level architecture

```
                        ┌────────────────────────────────────────────┐
                        │  GitHub Actions (scheduled, weekly, or     │
                        │  workflow_dispatch with `run_triage`)      │
                        │  .github/workflows/azdo-triage.yml         │
                        └────────────────────┬───────────────────────┘
                                             │
                                             ▼
        ╔═══════════════════════════════════════════════════════════════╗
        ║  Job 1 — `report`   (always runs)                             ║
        ║  ─────────────────────────────────────────────────────────    ║
        ║  WIQL → sanitize titles → create ONE summary issue in         ║
        ║  microsoft/PTVS titled                                        ║
        ║  "AzDO triage report YYYY-MM-DD to YYYY-MM-DD (<N>)".         ║
        ║  Body is a bullet list of links to the open WIs.              ║
        ║  Uploads candidates.json artifact for Job 2 to reuse.         ║
        ╚═════════════════════════════════════╤═════════════════════════╝
                                              │
                       if: run_triage != false (default true)
                                              │
                                              ▼
        ╔═════════════════════════════════════════════════════════════════════╗
        ║  Job 2 — Triage pipeline (conditional)                              ║
        ║                                                                     ║
        ║  Three GH-Actions jobs sharing the run_triage gate:                 ║
        ║                                                                     ║
        ║  ┌──────────────┐   ┌──────────────────┐   ┌─────────────────┐      ║
        ║  │ prepare:     │   │ triage:          │   │ apply:          │      ║
        ║  │ fetch ctx,   ├──▶│ matrix fan-out,  ├──▶│ apply outcomes  │      ║
        ║  │ parse diag,  │   │ AI triage per    │   │ (gated by       │      ║
        ║  │ sanitize,    │   │ cluster primary  │   │ azdo-triage-    │      ║
        ║  │ cluster      │   │                  │   │ approval env.)  │      ║
        ║  └──────────────┘   └──────────────────┘   └─────────────────┘      ║
        ║                                                                     ║
        ║  Per-primary outcome is one of:                                     ║
        ║   • Respond on AzDO and close (full resolution, not a link)         ║
        ║   • Mirror to microsoft/PTVS and close AzDO with link               ║
        ║   • Leave alone (low confidence or `not_actionable`)                ║
        ║   • Security route (private — no public mirror)                     ║
        ╚═════════════════════════════════════════════════════════════════════╝
```

Maintainer-driven fix path (out-of-band; not automated):

- Once a mirrored issue lands, a maintainer may invoke the Copilot
  cloud agent on it manually (assign to `Copilot` in the UI, or use
  the Azure Boards integration described in §6.4).
- The bridge does **not** auto-assign to Copilot — for a maintenance-mode
  project with limited PR-review capacity, drive-by Copilot PRs are
  noise rather than help.

Job 1's only write is the weekly report issue in `microsoft/PTVS`;
that's low-risk and ungated. Job 2's `apply` step writes to AzDO and
mirrors to GitHub — gated by a manual-approval
[environment](https://docs.github.com/en/actions/deployment/targeting-different-environments).

---

## 5. AI agent choice

| Option | Pros | Cons | Recommendation |
|---|---|---|---|
| **A. [`actions/ai-inference@v1`](https://github.com/actions/ai-inference) + GitHub Models** (`openai/gpt-4.1`) with `enable-github-mcp: true` and toolsets `repos,issues` | Native to GH Actions; free tier; supports prompt files & JSON schema; the `issues` MCP toolset lets the model read comments on a candidate duplicate during the same inference call, which is what enables the "apply the resolution, don't just link" pattern. | Single round-trip; no shell/file tools beyond MCP; GitHub Models is rate-limited and "not designed for production use cases" per docs. | **Use for the triage step.** Output is JSON `{verdict, response_md, github_issue_body_md, missing_info, related_urls, confidence, source_issue_for_resolution}`. |
| **B. GitHub Copilot CLI** via `provider: copilot` | Agentic — can run shell, read files, call web. | Per `actions/ai-inference` README, `enable-github-mcp`, `custom-headers`, `endpoint`, and `responseFormat`/`jsonSchema` are **ignored** under this provider — so we'd lose schema-constrained output. Premium-request cost; longer runtime. | Hold in reserve. Reach for it only if option A produces poor verdicts in dry-run. |
| **C. GitHub Copilot cloud agent** (assign GH issue to `Copilot`) | Produces an actual draft PR. | Premium-request cost; in maintenance mode the team has limited capacity to review drive-by Copilot PRs. | **Not in the automated path.** Maintainers invoke it manually (§6.3 Step 7, §6.4). |
| **D. Azure OpenAI / Azure AI Foundry agent in an AzDO pipeline** | Keeps everything inside DevDiv tenant. | More infra; harder to wire to GH issue creation. | Not recommended for v1. |

**Picked stack:** A. Option C remains available manually; option B held in reserve.

> ⚠️ All AI output is **drafted, not auto-committed**. v1 always requires
> human approval before any AzDO state change or GitHub mirror creation.

> ⚠️ **Caveat for option B**: per `actions/ai-inference` README, when
> `provider: copilot` is set, the JSON-schema response format keys are
> ignored. Our triage prompt depends on JSON-schema-constrained output,
> so a switch to provider B requires a separate prompt that asks for
> JSON in plain text and validates on our side.

---

## 6. Detailed component design

### 6.1 Workflow location & filename

- New file: `.github/workflows/azdo-triage.yml`.
- Supporting scripts: `Build/triage/` (new directory). Mirrors the
  existing convention of `Build/*.ps1` for tooling.
  - `Build/triage/query-azdo.ps1` — WIQL query + work item fetch.
    Used by **both** Job 1 (report) and Job 2 (triage prepare).
  - `Build/triage/post-report-issue.ps1` — Job 1's writer. Takes the
    sanitized candidate list and creates a single summary issue in
    `microsoft/PTVS` titled
    `AzDO triage report YYYY-MM-DD to YYYY-MM-DD (<N> open items)`,
    labelled `azdo-triage-report`, assigned to `StellaHuang95`.
    Body is a bullet list of `[AzDO #<id>](<url>) — <sanitized title>
    — created <date> — area: <subpath>` lines.
  - `Build/triage/fetch-context.ps1` — pulls the shared per-run
    research context the AI sees on every candidate: last ~90 days
    of PTVS commit messages (`git log`), the last three release
    tags, and the current set of issues labelled
    `fixed in next version`. Writes `$RUNNER_TEMP/run-context.json`.
    Fetched once per run. (Job 2 only.)
  - `Build/triage/parse-diagnostics.ps1` — for each candidate,
    downloads the attached `PythonToolsDiagnostics_*.log` (if any),
    extracts structured fields (VS version, PTVS version, Python
    version, loaded assemblies, last N exceptions), and emits a
    small sanitized JSON the AI consumes as `file_input`. Skips
    silently if no diagnostics file attached. (Job 2 only.)
  - `Build/triage/sanitize.ps1` — PII/secret redaction over the
    work-item body, comments, and the diagnostics extract. Job 1
    invokes a lightweight title-only mode (`-TitlesOnly`) to scrub
    the bullet list it posts. Job 2 invokes the full mode.
  - `Build/triage/cluster.ps1` — within-batch deduplication. Takes
    sanitized titles + first-paragraph summaries and asks the AI
    for a single small classification call
    (`{"clusters":[[id1,id2],[id3],...]}`). The first ID in each
    cluster is the "primary"; the others are "followers." Only
    primaries are run through full triage; followers' AzDO work
    items get closed with a comment pointing at the primary's
    mirrored GH issue. (Job 2 only.)
  - `Build/triage/post-azdo.ps1` — comment + close work item. (Job 2 only.)
  - `Build/triage/mirror-to-github.ps1` — create issue in
    `microsoft/PTVS`. (Job 2 only.)
  - `Build/triage/apply-outcomes.ps1` — reads verdicts + cluster
    map, dispatches the per-candidate actions in the right order.
    (Job 2 only.)
  - `Build/triage/prompts/triage.prompt.yml` — system + user prompt
    consumed by `actions/ai-inference`. Same prompt produces verdict
    AND draft customer response (via `response_md`).
  - `Build/triage/prompts/cluster.prompt.yml` — small prompt used by
    `cluster.ps1`.
  - `Build/triage/config.json` — confidence thresholds, label
    whitelist for the AI to emit, max-candidates cap, and the list
    of upstream issue trackers (Pylance, debugpy, cpython) the AI
    should reference in `related_urls`.

PowerShell is chosen because the repo already uses PS for build
automation ([Build/PreBuild.ps1](Build/PreBuild.ps1),
[Build/Get_Azdo_Build.ps1](Build/Get_Azdo_Build.ps1),
[Build/Run_Azdo_Pipeline.ps1](Build/Run_Azdo_Pipeline.ps1),
[Build/ZipSource.ps1](Build/ZipSource.ps1)). Node/Python would also
work; PS keeps the toolset consistent.

**Testing**: The repo does **not** currently use Pester or any
PowerShell test framework (verified — no `*.Tests.ps1` exists). Two
options, ranked by preference:

1. **(Preferred for v1) Lightweight in-script smoke tests** — each
   script accepts a `-SelfTest` switch that runs a few inline
   assertions against canned input/output JSON fixtures stored under
   `Build/triage/tests/fixtures/`. A single workflow job
   `triage-tests` runs `pwsh -File Build/triage/run-tests.ps1` on
   every PR that touches `Build/triage/**`. No new framework.
2. **(Future) Pester** — adopt only if/when the test surface grows.

Minimum coverage:

- `sanitize.ps1`: emails / paths / common secret regexes are
  redacted; unchanged for non-PII input; raises on detected
  unredacted secret.
- `query-azdo.ps1`: WIQL string is well-formed; parser handles empty
  result; pagination follows `continuationToken`.
- `apply-outcomes.ps1`: dry-run path emits the expected Markdown
  summary; idempotent rerun does not double-create when a matching
  `[AzDO #<id>]` issue already exists (open or closed).
- `post-report-issue.ps1`: handles empty candidate list (no issue
  filed); generates correct title/body for typical cases.

### 6.2 Workflow skeleton (illustrative — not yet committed)

```yaml
name: AzDO weekly triage

on:
  schedule:
    - cron: "0 8 * * 1"   # every Monday 08:00 UTC
  workflow_dispatch:
    inputs:
      lookback_days:
        description: "How many days back to scan (default 7)"
        required: false
        default: "7"
      workitem_id:
        description: "If set, triage only this single AzDO work item ID (for ad-hoc replay)"
        required: false
      dry_run:
        description: "If true, do not write to AzDO or create GitHub mirrored issues"
        required: false
        default: "true"
        type: boolean
      max_candidates:
        description: "Hard cap on candidates processed per run (cost-safety)"
        required: false
        default: "25"
      run_triage:
        description: "Run Job 2 (triage pipeline). Uncheck to run only Job 1 (the weekly summary report)."
        required: false
        default: true
        type: boolean

permissions:
  contents: read
  issues: write          # to create issues in this repo
  models: read           # for actions/ai-inference
  id-token: write        # for OIDC -> Entra federated credential

concurrency:
  group: azdo-triage
  cancel-in-progress: false

jobs:
  # ════════════════════════════════════════════════════════════════
  # JOB 1 — Weekly report (ALWAYS runs).
  # Posts a single summary issue in microsoft/PTVS listing every
  # open AzDO work item created in the lookback window. Also
  # uploads candidates.json for Job 2 to reuse.
  # ════════════════════════════════════════════════════════════════
  report:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Acquire Entra token via GitHub OIDC
        uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZDO_TRIAGE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZDO_TRIAGE_TENANT_ID }}
          allow-no-subscriptions: true

      - name: Exchange for AzDO access token
        shell: bash
        run: |
          # 499b84ac-... is the well-known AzDO resource AppId.
          # azure/login does NOT itself emit an AzDO-scoped bearer
          # token — we mint one via the now-authenticated `az` CLI.
          AZDO_ACCESS_TOKEN=$(az account get-access-token \
            --resource 499b84ac-1321-427f-aa17-267ca6975798 \
            --query accessToken -o tsv)
          echo "::add-mask::$AZDO_ACCESS_TOKEN"
          echo "AZDO_ACCESS_TOKEN=$AZDO_ACCESS_TOKEN" >> "$GITHUB_ENV"

      - name: Query AzDO work items (WIQL)
        shell: pwsh
        env:
          LOOKBACK_DAYS:  ${{ inputs.lookback_days || '7' }}
          MAX_CANDIDATES: ${{ inputs.max_candidates || '25' }}
        run: |
          ./Build/triage/query-azdo.ps1 `
            -OutFile $env:RUNNER_TEMP/candidates.json `
            -LookbackDays $env:LOOKBACK_DAYS `
            -MaxCandidates $env:MAX_CANDIDATES

      - name: Sanitize titles only (for the public summary issue)
        shell: pwsh
        run: |
          ./Build/triage/sanitize.ps1 `
            -TitlesOnly `
            -InFile  $env:RUNNER_TEMP/candidates.json `
            -OutFile $env:RUNNER_TEMP/candidates-titles-only.json

      - name: Post weekly report issue
        shell: pwsh
        env:
          PTVS_BRIDGE_PAT: ${{ secrets.PTVS_BRIDGE_PAT }}
          LOOKBACK_DAYS:   ${{ inputs.lookback_days || '7' }}
          TRIAGE_WILL_RUN: ${{ inputs.run_triage != false }}
          RUN_URL: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}
        run: |
          ./Build/triage/post-report-issue.ps1 `
            -CandidatesFile $env:RUNNER_TEMP/candidates-titles-only.json `
            -LookbackDays   $env:LOOKBACK_DAYS `
            -TriageWillRun  ([bool]::Parse($env:TRIAGE_WILL_RUN)) `
            -RunUrl         $env:RUN_URL

      - name: Upload candidates artifact for Job 2
        uses: actions/upload-artifact@v4
        with:
          name: candidates
          path: ${{ runner.temp }}/candidates.json
          retention-days: 7   # short retention; contains raw WI fields

  # ════════════════════════════════════════════════════════════════
  # JOB 2 — Triage pipeline (CONDITIONAL on run_triage).
  # Implemented as 3 GH-Actions jobs (prepare → triage → apply).
  # All three share the same run_triage gate so they're toggled
  # atomically.
  # ════════════════════════════════════════════════════════════════
  prepare:
    needs: report
    if: ${{ inputs.run_triage != false }}
    runs-on: ubuntu-latest
    outputs:
      # IMPORTANT: matrix output is a JSON array of {id} objects
      # ONLY, and only for cluster primaries. Full sanitized
      # work-item content and the cluster map live in artifacts.
      # GH Actions outputs are capped at 1 MB and matrix at 256 jobs.
      primaries: ${{ steps.cluster.outputs.primaries }}
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0   # fetch-context.ps1 needs ~90d of git log

      - uses: actions/download-artifact@v4
        with:
          name: candidates
          path: ${{ runner.temp }}

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZDO_TRIAGE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZDO_TRIAGE_TENANT_ID }}
          allow-no-subscriptions: true

      - name: Exchange for AzDO access token
        shell: bash
        run: |
          AZDO_ACCESS_TOKEN=$(az account get-access-token \
            --resource 499b84ac-1321-427f-aa17-267ca6975798 \
            --query accessToken -o tsv)
          echo "::add-mask::$AZDO_ACCESS_TOKEN"
          echo "AZDO_ACCESS_TOKEN=$AZDO_ACCESS_TOKEN" >> "$GITHUB_ENV"

      - name: Fetch shared per-run research context
        shell: pwsh
        env:
          PTVS_TRIAGE_MCP_READONLY_PAT: ${{ secrets.PTVS_TRIAGE_MCP_READONLY_PAT }}
        run: |
          ./Build/triage/fetch-context.ps1 `
            -OutFile $env:RUNNER_TEMP/run-context.json

      - name: Download and parse diagnostics attachments
        shell: pwsh
        run: |
          ./Build/triage/parse-diagnostics.ps1 `
            -CandidatesFile $env:RUNNER_TEMP/candidates.json `
            -OutDir         $env:RUNNER_TEMP

      - name: Sanitize all candidates (incl. parsed diagnostics)
        shell: pwsh
        run: |
          ./Build/triage/sanitize.ps1 `
            -InFile  $env:RUNNER_TEMP/candidates.json `
            -DiagDir $env:RUNNER_TEMP `
            -OutDir  $env:RUNNER_TEMP/sanitized

      - name: Cluster within-batch duplicates
        id: cluster
        shell: pwsh
        run: |
          ./Build/triage/cluster.ps1 `
            -SanitizedDir $env:RUNNER_TEMP/sanitized `
            -ClustersOut  $env:RUNNER_TEMP/clusters.json `
            -PrimariesOut $env:RUNNER_TEMP/primaries.json
          $primaries = Get-Content $env:RUNNER_TEMP/primaries.json -Raw
          echo "primaries=$primaries" >> $env:GITHUB_OUTPUT

      - name: Upload run artifacts
        uses: actions/upload-artifact@v4
        with:
          name: run-artifacts
          path: |
            ${{ runner.temp }}/run-context.json
            ${{ runner.temp }}/clusters.json
            ${{ runner.temp }}/sanitized/
          retention-days: 7

  triage:
    needs: prepare
    if: ${{ inputs.run_triage != false }}
    runs-on: ubuntu-latest
    strategy:
      fail-fast: false
      max-parallel: 3
      matrix:
        # only cluster PRIMARIES flow through the matrix; followers
        # are handled by apply-outcomes.ps1 in the apply job.
        candidate: ${{ fromJson(needs.prepare.outputs.primaries) }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/download-artifact@v4
        with:
          name: run-artifacts
          path: ${{ runner.temp }}

      - name: Triage with AI
        id: triage
        uses: actions/ai-inference@v1
        with:
          prompt-file: ./Build/triage/prompts/triage.prompt.yml
          input: |
            workitem_id: ${{ matrix.candidate.id }}
          file_input: |
            workitem_json: ${{ runner.temp }}/sanitized/wi-${{ matrix.candidate.id }}.json
            diagnostics_json: ${{ runner.temp }}/sanitized/diag-${{ matrix.candidate.id }}.json
            run_context_json: ${{ runner.temp }}/run-context.json
          enable-github-mcp: true
          # READ-ONLY token, separate from PTVS_BRIDGE_PAT. Limits
          # blast radius if the model is induced to misuse MCP.
          github-mcp-token: ${{ secrets.PTVS_TRIAGE_MCP_READONLY_PAT }}
          # `issues` toolset lets the model read comments on a
          # candidate duplicate — what makes "apply the resolution,
          # don't just link" possible.
          github-mcp-toolsets: 'repos,issues'
          model: openai/gpt-4.1
          max-completion-tokens: 4000

      - name: Persist verdict
        # NOTE: `outputs.response` may contain newlines and quotes;
        # `outputs.response-file` is the safe way to capture it.
        shell: bash
        run: |
          cp "${{ steps.triage.outputs.response-file }}" \
             "$RUNNER_TEMP/verdict-${{ matrix.candidate.id }}.json"

      - uses: actions/upload-artifact@v4
        with:
          name: verdicts
          path: ${{ runner.temp }}/verdict-*.json
          retention-days: 30

  apply:
    needs: triage
    if: ${{ inputs.run_triage != false }}
    runs-on: ubuntu-latest
    environment: azdo-triage-approval   # ← human approval gate
    steps:
      - uses: actions/checkout@v4
      - uses: actions/download-artifact@v4
        with: { name: verdicts,      path: ${{ runner.temp }} }
      - uses: actions/download-artifact@v4
        with: { name: run-artifacts, path: ${{ runner.temp }} }
      - uses: actions/download-artifact@v4
        with: { name: candidates,    path: ${{ runner.temp }} }

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZDO_TRIAGE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZDO_TRIAGE_TENANT_ID }}
          allow-no-subscriptions: true

      - name: Exchange for AzDO access token
        shell: bash
        run: |
          AZDO_ACCESS_TOKEN=$(az account get-access-token \
            --resource 499b84ac-1321-427f-aa17-267ca6975798 \
            --query accessToken -o tsv)
          echo "::add-mask::$AZDO_ACCESS_TOKEN"
          echo "AZDO_ACCESS_TOKEN=$AZDO_ACCESS_TOKEN" >> "$GITHUB_ENV"

      - name: Apply outcomes
        shell: pwsh
        env:
          DRY_RUN: ${{ inputs.dry_run }}
          PTVS_BRIDGE_PAT: ${{ secrets.PTVS_BRIDGE_PAT }}
        run: ./Build/triage/apply-outcomes.ps1
              -VerdictsDir   $env:RUNNER_TEMP
              -RunArtifacts  $env:RUNNER_TEMP

      - name: Upload run summary (no PII — markdown of verdicts only)
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: run-summary
          path: ${{ runner.temp }}/run-summary.md
          retention-days: 90

  notify-on-failure:
    needs: [report, prepare, triage, apply]
    if: ${{ always() && (needs.report.result == 'failure' || needs.prepare.result == 'failure' || needs.triage.result == 'failure' || needs.apply.result == 'failure') }}
    runs-on: ubuntu-latest
    permissions:
      issues: write
    steps:
      - uses: actions/github-script@v7
        with:
          script: |
            await github.rest.issues.create({
              owner: context.repo.owner,
              repo: context.repo.repo,
              title: `azdo-triage workflow failed (run ${context.runId})`,
              body: `One of the azdo-triage jobs failed. See ${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId}.`,
              labels: ['infrastructure'],
              assignees: ['StellaHuang95']
            });
```

> The exact YAML above is illustrative. `azure/login@v2` exchanges
> the GitHub OIDC token for an Entra access token via the
> `api://AzureADTokenExchange` audience and authenticates `az`. It
> does **not** itself emit an AzDO-scoped bearer token — the
> follow-on step
> `az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798`
> mints the AzDO-resource token. Scripts then call AzDO REST with
> `Bearer $AZDO_ACCESS_TOKEN`.

### 6.3 Step-by-step behavior

#### Step 1 — `query-azdo.ps1`

WIQL:

```sql
SELECT [System.Id]
FROM   workitems
WHERE  [System.TeamProject] = 'DevDiv'
  AND  [System.AreaPath] UNDER 'DevDiv\Python and AI Tools'
  AND  [System.WorkItemType] IN ('Bug', 'Task', 'User Story')
  AND  [System.CreatedDate] >= @Today - 7
  AND  [System.State] <> 'Closed'
  AND  [System.State] <> 'Removed'
  AND  NOT [System.Tags] CONTAINS 'triaged-by-ai'
  AND  NOT [System.Tags] CONTAINS 'do-not-triage'
ORDER BY [System.CreatedDate] ASC
```

- `triaged-by-ai` is the marker stamped after a candidate has been
  processed (regardless of verdict). Prevents accidental re-triage
  on the next weekly run if a candidate was left open intentionally.
- `do-not-triage` is a manual escape hatch a maintainer can apply on
  a work item to permanently opt it out of automation.
- The `workflow_dispatch` `workitem_id` input bypasses the date
  filter and forces processing of one explicit ID.

Implementation notes:

- Endpoint: `POST https://dev.azure.com/devdiv/DevDiv/_apis/wit/wiql?api-version=7.1`.
- The response only contains IDs. Batch-fetch full details:
  `POST https://dev.azure.com/devdiv/_apis/wit/workitemsbatch?api-version=7.1`
  with `{"ids": [...], "$expand": "All", "fields": [...]}`.
- For each work item, also fetch:
  - `GET .../wit/workItems/{id}/comments?api-version=7.0-preview.3`
  - For each `AttachedFile` relation: `GET .../wit/attachments/{guid}`
    (metadata only in this step; bytes only if mirroring).
- Output: a JSON array, one element per candidate (id, title,
  description_html, repro_steps_html, system_info, tags, area_path,
  created_by_display, created_by_email, created_date, state, url,
  comment_count, attachments[], rev).

#### Step 2 — `fetch-context.ps1` (per-run, not per-candidate)

Builds a single small JSON the AI gets on every candidate:

- `recent_commits`: `git log --since=90.days --pretty=format:'%h %s'`
  filtered to changed files of interest (Python/Product/**,
  Common/**, excluding Build/**). Capped at ~150 entries.
- `recent_releases`: last 3 release tags + their dates.
- `fixed_in_next_version`: titles + numbers of open PTVS issues
  labelled `fixed in next version` (via GitHub MCP read-only).
- `area_path_history`: last 90 days of AzDO work items under the
  same area path subtree, titles + IDs + state only — the
  "has-this-been-reported-before" lookup. Capped at ~200.
- `upstream_tracker_urls`: hard-coded list from `config.json`
  (Pylance, debugpy, cpython issue URLs) so the AI can cite them
  without inventing addresses.

#### Step 3 — `parse-diagnostics.ps1` (per candidate)

If a candidate has a `PythonToolsDiagnostics_*.log` attachment,
download it via the AzDO attachment API, parse out the structured
header (VS version, PTVS version, Python version, loaded assemblies,
last exceptions), and write `diag-<id>.json`. Skips silently if no
attachment.

#### Step 4 — `sanitize.ps1`

Strip / mask before *anything* AI-related runs **and** before any
data goes to a public GitHub issue. Patterns:

- Email addresses → `<redacted-email>` (preserve domain only if
  needed for routing).
- Windows usernames / local paths → `C:\Users\jdoe\AppData\...`
  becomes `C:\Users\<redacted-user>\AppData\...`.
- Machine names / SIDs.
- Anything matching common secret regexes (PATs, JWTs, connection
  strings) — fail loudly if a match is found in customer content;
  do not mirror.
- Customer telephone numbers, addresses (unlikely but possible in
  diagnostic logs).

Run `microsoft/security-devops-action` (CredScan) over the
sanitized artifact as a second-line check before any public posting
(PTVS already runs CredScan in
[azure-pipelines-compliance.yml](azure-pipelines-compliance.yml)).

Attachments are **not** auto-uploaded to GitHub — they are linked
back to the AzDO work item only.

The `-TitlesOnly` mode (used by Job 1) does a minimal pass: scrubs
emails and Windows-username path fragments from each title, leaves
the rest of the work item untouched.

#### Step 5 — AI triage

`Build/triage/prompts/triage.prompt.yml`:

```yaml
messages:
  - role: system
    content: |
      You are a triage assistant for the Python Tools for Visual Studio
      (PTVS) team. PTVS is in maintenance mode; the team's capacity for
      drive-by issues is limited, so prefer outcomes that close the loop
      with the customer over outcomes that just route work to the team.

      The user provides:
        * a sanitized AzDO work item (customer bug report),
        * a parsed PythonToolsDiagnostics_*.log if attached (key
          environment fields, last exceptions),
        * a per-run research context (recent PTVS commits, recent
          releases, current `fixed in next version` issues, last 90
          days of related AzDO work items, and the list of upstream
          trackers).

      Your job is to:

      1. Classify the report into one of:
         { answered, needs_info, real_bug, real_feature_request,
           not_actionable, security }.

         - `answered` covers both "we already know the answer" and
           "duplicate of an existing report". When you set `answered`,
           you MUST also do step 2 below. (We treat duplicates by
           applying their resolution, not by citing them.)
         - `security` is for anything that smells like a CVE / RCE /
           auth bypass / pre-disclosure security report. Set
           `security` and stop — we never publish security reports.

      2. If `answered`, draft a concise, friendly customer response
         (markdown, < 250 words) that gives the customer the **actual
         resolution**, not a link to a duplicate. Concretely:
            (a) Identify the most likely matching prior issue (PTVS
                GitHub, recent AzDO history, or a `fixed in next
                version` candidate).
            (b) Use the GitHub MCP `issues` toolset to fetch the
                comments on that matching issue and read its
                resolution.
            (c) Adapt that resolution to the current customer's
                context (their Python version, VS version, etc. from
                the diagnostics extract) and inline it in
                `response_md`. The customer should be able to act on
                the message without clicking anything.
            (d) If the matching issue is `fixed in next version`,
                say which version, and when (release tag/date from
                run_context_json) — don't just link.
            (e) Cite the source issue as a single trailing line so a
                maintainer can audit: `_Resolution adapted from #N._`

      3. If `needs_info`, list exactly which pieces of information
         are missing (VS version, PTVS version, Python version,
         diagnostics file, repro steps). The missing-info request
         will be served on GitHub — populate `github_issue_body_md`
         as well, because the apply step will mirror needs_info
         reports to GitHub with the `waiting for response` label.

      4. If `real_bug` or `real_feature_request`, draft a
         GitHub-flavored issue body using the PTVS bug-report
         template. Populate `github_issue_body_md`.

      5. Do not invent URLs, issue numbers, or version strings. Only
         cite values that appear in the inputs or that you retrieved
         via the GitHub MCP server.

      6. Always return strict JSON conforming to the response schema.

  - role: user
    content: |
      Work item ID: {{workitem_id}}
      ---
      Work item (sanitized):
      {{workitem_json}}
      ---
      Parsed diagnostics (may be empty):
      {{diagnostics_json}}
      ---
      Per-run research context:
      {{run_context_json}}

model: openai/gpt-4.1
responseFormat: json_schema
jsonSchema: |
  {
    "name": "triage_verdict",
    "strict": true,
    "schema": {
      "type": "object",
      "additionalProperties": false,
      "required": ["verdict", "confidence", "response_md", "github_issue_body_md", "missing_info", "related_urls", "source_issue_for_resolution"],
      "properties": {
        "verdict": {
          "type": "string",
          "enum": ["answered","needs_info","real_bug","real_feature_request","not_actionable","security"]
        },
        "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
        "response_md": { "type": "string", "description": "Customer-facing AzDO comment when verdict is `answered`. Empty otherwise (the GitHub issue body carries the message in mirroring outcomes)." },
        "github_issue_body_md": { "type": "string", "description": "Body of the mirrored GitHub issue. Required for needs_info / real_bug / real_feature_request." },
        "missing_info": { "type": "array", "items": { "type": "string" } },
        "related_urls": { "type": "array", "items": { "type": "string" } },
        "source_issue_for_resolution": { "type": "string", "description": "Empty unless verdict == answered. Format: `#N` or full URL." }
      }
    }
  }
modelParameters:
  temperature: 0.2
  maxCompletionTokens: 4000
```

With `enable-github-mcp: true` + `github-mcp-toolsets: 'repos,issues'`,
the model can both search PTVS issues for matches and fetch the
comments on them to read the actual resolution. This is what enables
the "apply the resolution, don't just link" pattern.

#### Step 6 — Apply outcomes (`apply-outcomes.ps1`)

This script reads the verdict JSON for each cluster primary, the
cluster map, and dispatches.

> **Important — these are Developer Community feedback tickets.**
> Work items under `DevDiv\Python and AI Tools\**` are typically
> mirrors of customer feedback filed on
> [Developer Community](https://developercommunity.visualstudio.com)
> (the public VS feedback portal — `developercommunity.visualstudio.com`).
> DC has its own state machine and duplicate-with-vote-transfer
> workflow that the customer recognizes from other VS feedback they
> may have filed; we integrate with that workflow instead of
> inventing our own. Specifically:
>
> - The state we transition into for "mirror-to-GitHub" outcomes is
>   **`DC - Closed - Duplicated`**, not a generic `Closed`.
> - We populate the **`Duplicate Feedback Ticket ID`** field
>   (visible on the work item's Overview tab) with the new GitHub
>   issue URL so DC can transfer votes/follows from the original
>   feedback to the linked ticket.
> - We post the canned DC duplicate template as a comment so the
>   customer sees the same friendly "we've transferred your votes;
>   please follow along on the linked ticket" wording they're used
>   to from other VS feedback.
>
> **Phase 0 must verify**: (a) the exact REST field name behind
> "Duplicate Feedback Ticket ID" on the Overview tab (referred to
> below as `<DuplicateFeedbackTicketIdField>` — likely something
> like `Microsoft.VSTS.Common.DuplicateFeedbackTicketId` or a
> DevDiv-custom field), and (b) the exact state-value strings
> available on these work items, by fetching one example item with
> `?$expand=all` and inspecting `fields` and `_links.workItemType`.

| Verdict | Confidence | Action |
|---|---|---|
| `answered` | ≥ 0.7 | Post `response_md` as a comment on the AzDO work item (the comment contains the *full* resolution, adapted from `source_issue_for_resolution` — not just a link). Close using the DC "fixed/answered" state (likely `DC - Closed - Other` or `DC - Closed - Fixed` if the resolution is a `fixed in next version` citation — exact value to confirm in phase 0). Tag `triaged-by-ai`. **Do not mirror to GitHub.** |
| `needs_info` | ≥ 0.6 | **Mirror to GitHub.** Create issue at `microsoft/PTVS` with `github_issue_body_md`, including a "What we need from you" bullet list built from `missing_info`. Labels: `bug, needs repro, from-azdo, waiting for response`. Then close the AzDO work item using the **DC duplicate flow** (see below): state `DC - Closed - Duplicated`, `Duplicate Feedback Ticket ID` = the new GH issue URL, plus the canned DC duplicate template as a comment. |
| `real_bug`, `real_feature_request` | ≥ 0.6 | Mirror to GitHub with `github_issue_body_md`. Labels: `bug, needs repro, from-azdo` (no `waiting for response`). Close the AzDO work item using the **DC duplicate flow** (state `DC - Closed - Duplicated`, populate `Duplicate Feedback Ticket ID`, post canned template). |
| `not_actionable` | any | Do nothing automatically; flag for human review. |
| `security` | any | Never mirror. Private notification (email maintainer or open private security advisory). Do not modify the AzDO work item — let MSRC handle it. |
| any | < threshold | Do nothing automatically; flag for human review. |

**Cluster followers.** For every follower of a primary with a
mirroring outcome, the apply step closes the follower using the
same DC duplicate flow, pointing at the primary's mirrored GH
issue (state `DC - Closed - Duplicated`, `Duplicate Feedback
Ticket ID` = primary's GH issue URL, canned template). Followers
of an `answered` primary get the same `response_md` posted as a
comment and are closed independently (DC answered state).

All of the above only execute when `inputs.dry_run` is `false`
**and** the `azdo-triage-approval` environment has been approved.

**Confidence thresholds (0.7 / 0.6) are initial guesses.** They will
be tuned during phase 1 by comparing AI verdicts against the
maintainer's actual disposition. Thresholds live in
`Build/triage/config.json`.

**Crash-safe ordering of writes (per candidate).** When a verdict
requires multiple side effects, they are applied in this order:

1. Create the GitHub issue (idempotent: before creating, search for
   an existing issue — open **or closed** — with title prefix
   `[AzDO #<id>]`).
2. Post the AzDO comment with the DC duplicate template (which
   contains the GH issue URL).
3. Patch the AzDO work item: state → `DC - Closed - Duplicated`,
   `Duplicate Feedback Ticket ID` → GH issue URL, tag
   `triaged-by-ai`.

If step 1 fails, nothing else runs. If step 2 fails after step 1,
we retry step 2 on the next run (the AzDO work item is still open,
but the GH issue is discoverable so we don't double-create). If
step 3 fails after step 2, the AzDO work item stays open but
already has the comment; next run skips it because the
`triaged-by-ai` tag was applied alongside the comment in step 2.

> **Reopen behavior.** Closing in DC is reversible — the customer
> or a maintainer can move the state back out of `DC - Closed - *`.
> The `triaged-by-ai` tag serves as a "needs human attention if
> reopened" marker so we don't auto-close again. The WIQL filter
> (`NOT [System.Tags] CONTAINS 'triaged-by-ai'`) excludes it.

**DC duplicate-close AzDO write API calls (mirroring outcomes):**

```http
PATCH https://dev.azure.com/devdiv/DevDiv/_apis/wit/workitems/{id}?api-version=7.1
Content-Type: application/json-patch+json

[
  { "op": "test", "path": "/rev", "value": <CURRENT_REV> },
  { "op": "add",  "path": "/fields/System.State",
                  "value": "DC - Closed - Duplicated" },
  { "op": "add",  "path": "/fields/<DuplicateFeedbackTicketIdField>",
                  "value": "https://github.com/microsoft/PTVS/issues/12345" },
  { "op": "add",  "path": "/fields/System.Tags",
                  "value": "triaged-by-ai; moved-to-github" }
]
```

> ⚠️ `<CURRENT_REV>` **must be read at apply-time** from the work
> item response we just fetched (don't hard-code `1`). If the PATCH
> returns HTTP 409 (rev mismatch), log and skip — a human may have
> edited the WI in the meantime.
>
> ⚠️ `<DuplicateFeedbackTicketIdField>` is a placeholder for the
> exact REST field name backing the "Duplicate Feedback Ticket ID"
> Overview-tab field on these DC work items. Verify the exact name
> in phase 0 by fetching one item and inspecting its `fields`.

**Comment posted to the customer (canned DC duplicate template):**

```http
POST https://dev.azure.com/devdiv/DevDiv/_apis/wit/workItems/{id}/comments?api-version=7.0-preview.3
Content-Type: application/json

{
  "text": "Thanks for taking the time to report this issue! It seems to us that it's describing the same thing as [another one](https://github.com/microsoft/PTVS/issues/12345). We've closed this one as a duplicate and transferred all votes from this ticket to the other one. Please follow along on the linked ticket to communicate with the team, see updates on status, and help provide any needed diagnostic info. For more information, see our [issue reporting guidelines](https://aka.ms/vsfeedbackguidelines).\n\nHappy coding!"
}
```

This is the standard VS feedback duplicate template that customers
already recognize from other DC interactions. The single link
inside the template is the URL of the newly-created mirror issue
on `microsoft/PTVS`. The "transferred all votes" wording is
literal — populating `Duplicate Feedback Ticket ID` is what tells
the DC backend to do the transfer.

GitHub issue creation:

```http
POST https://api.github.com/repos/microsoft/PTVS/issues
Authorization: Bearer <PTVS_BRIDGE_PAT>
Content-Type: application/json

{
  "title": "<title prefixed with [AzDO #2711586]>",
  "body":  "<github_issue_body_md, with frontmatter linking AzDO URL>",
  "labels": [
    "bug",
    "needs repro",
    "from-azdo"
  ],
  "assignees": ["StellaHuang95"]
}
```

> **Label inventory check** (verified against `gh label list --repo
> microsoft/PTVS`): `bug`, `needs repro`, `enhancement`, and
> `waiting for response` already exist. `from-azdo` and
> `azdo-triage-report` are **new** and must be created once before
> first non-dry-run. The `waiting for response` label is the key
> integration with existing repo workflows:
> [auto-label.yml](.github/workflows/auto-label.yml) automatically
> flips it to `user responded` when the customer replies, and
> [stale.yml](.github/workflows/stale.yml) closes it after 30 days
> of silence — so the bridge doesn't need to invent its own
> follow-up logic for `needs_info` items. AI-emitted label set is
> restricted to a whitelist in `Build/triage/config.json`.

The issue body always starts with a fenced metadata block so the
link back is unambiguous:

```markdown
> _Mirrored from internal report
> [`DevDiv#2711586`](https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2711586).
> Original reporter has been notified._
```

#### Step 7 — (manual, not automated) Hand off to Copilot cloud agent

> **The bridge does not automate this step.** Auto-assigning Copilot
> to every mirrored bug produces drive-by PRs the team has limited
> capacity to review.

When a maintainer decides to delegate a mirrored issue to Copilot
cloud agent, two manual paths:

1. From `microsoft/PTVS` on github.com: open the issue, click
   "Assignees" → "Copilot".
2. From the AzDO work item (before the bridge has closed it): the
   [Azure Boards Copilot integration](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/integrate-cloud-agent-with-azure-boards)
   described in §6.4.

For reference, the REST API call under the hood:

```http
POST https://api.github.com/repos/microsoft/PTVS/issues/{newId}/assignees
Accept: application/vnd.github+json
X-GitHub-Api-Version: 2022-11-28

{
  "assignees": ["copilot-swe-agent[bot]"],
  "agent_assignment": {
    "target_repo": "microsoft/PTVS",
    "base_branch": "main",
    "custom_instructions": "Reproduce the steps in the issue body. Propose a minimal, targeted fix. Keep the diff small and add a test if feasible."
  }
}
```

(REST literal is `copilot-swe-agent[bot]`; GraphQL is
`copilot-swe-agent` without the `[bot]` suffix.)

### 6.4 Related: Copilot cloud agent's Azure Boards integration

GitHub Copilot cloud agent ships a native [Azure Boards
integration](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/integrate-cloud-agent-with-azure-boards)
that lets a maintainer click "Create a pull request with Copilot" on
an AzDO work item, after which the agent opens a draft PR in the
linked GitHub repo. This is *not* a substitute for the bridge — it
does not triage, draft a customer response, close the AzDO work
item, or mirror anything publicly. But it's a useful manual escape
hatch. Install the Azure Boards GitHub App against `microsoft/PTVS`
regardless of whether this pipeline ships.

---

## 7. Authentication & secrets

### 7.1 Auth to Azure DevOps (read + write)

**Recommended (no static secret):**

- Create an Entra **service principal** in the
  `microsoft.onmicrosoft.com` tenant (DevDiv's tenant).
- Add it to the DevDiv AzDO organization
  (`https://dev.azure.com/devdiv`) as a member, **Basic** access level.
- Grant it permission to read/write work items under the
  `DevDiv\Python and AI Tools` area path only (least-privilege).
- Configure a **federated credential** on the SP that trusts
  `repo:microsoft/PTVS:environment:azdo-triage-approval` (issuer
  `https://token.actions.githubusercontent.com`, audience
  `api://AzureADTokenExchange`).
- `azure/login@v2` exchanges the GitHub OIDC token for an Entra
  token and authenticates the `az` CLI. The pipeline then runs
  `az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798`
  to obtain a bearer token usable against the AzDO REST API.
- No PAT stored anywhere. Tokens are short-lived (~1 h).

**Fallback (if SP can't be approved quickly):**

- Use a personal AzDO PAT with `vso.work_write` scope (90 day expiry).
- Store as `AZDO_PAT` repo secret. `Authorization: Basic $(":$pat" | base64)`.
- Rotate before expiry.

### 7.2 Auth to GitHub (cross-repo write)

- `GITHUB_TOKEN` cannot write to other repos. We need either:
  - A **fine-grained PAT** (`PTVS_BRIDGE_PAT`) owned by a service
    account with scopes `issues:write`, `metadata:read` on
    `microsoft/PTVS` only. Rotate quarterly.
  - **Or** a dedicated GitHub App installed on `microsoft/PTVS` with
    `issues:write`. Strongly preferred long-term.

### 7.3 Repo secrets to provision

| Secret | Purpose | Owner |
|---|---|---|
| `AZDO_TRIAGE_CLIENT_ID` | Entra SP client ID | dev infra |
| `AZDO_TRIAGE_TENANT_ID` | Entra tenant ID | dev infra |
| `PTVS_BRIDGE_PAT` *or* `PTVS_BRIDGE_APP_ID` + `PTVS_BRIDGE_APP_KEY` | GitHub issue **write** (`issues:write` + `metadata:read` on `microsoft/PTVS` only) | maintainers |
| `PTVS_TRIAGE_MCP_READONLY_PAT` | **Read-only** token passed to `actions/ai-inference`'s GitHub MCP server. Scoped to `metadata:read` + `issues:read` on `microsoft/PTVS`. Kept separate from `PTVS_BRIDGE_PAT`. | maintainers |
| `COPILOT_PAT` *(optional)* | Required only if we ever use `provider: copilot` | maintainer |
| `AZDO_PAT` *(fallback only)* | PAT in case SP path is delayed | maintainer |

> All secrets are referenced only by name in the workflow; values
> never appear in logs (GitHub Actions automatically masks secret
> values).

---

## 8. Rate limits, cost, and reliability

- **GitHub Models** rate limits: typical week's volume (< 30 issues /
  week × 2 inferences = ~60 calls) is well inside the free tier per
  docs. If we exceed, fall back to throttled mode (sleep between
  candidates).
- **AzDO REST**: per-user limits per the
  [AzDO rate-limits doc](https://learn.microsoft.com/en-us/azure/devops/integrate/concepts/rate-limits).
  A weekly batch is trivial.
- **GitHub Actions minutes**: scheduled job estimated at < 5 min
  wall clock for a typical week.
- **Failure handling**:
  - WIQL or REST 5xx → retry with exponential backoff, max 3 tries.
  - AI inference failure → log error, mark candidate
    `manual-triage-needed`, do **not** close the AzDO work item.
  - Sanitization detects unredacted secret/PII → abort that
    candidate, open a private notice rather than a public GitHub
    issue.
  - Concurrency: `concurrency.group: azdo-triage` ensures only one
    run at a time.

---

## 9. Privacy & safety

### 9.1 Risks

1. **Model-provider data handling**: `actions/ai-inference` with
   GitHub Models routes sanitized work-item content to GitHub's
   hosted model endpoints. Verify the data-sharing setting on the
   `microsoft` enterprise before enabling.
2. **Public disclosure of PII**: AzDO work items routinely contain
   customer email addresses, usernames, machine names, file paths,
   and diagnostic logs. Mirroring naively to `microsoft/PTVS`
   (public) would constitute a privacy incident.
3. **Public disclosure of pre-disclosure security info**: A customer
   may accidentally file a security bug as a normal bug. The
   pipeline must detect and route those reports to MSRC.
4. **Wrong auto-close**: Closing a customer's report with an
   incorrect or curt AI response damages trust.
5. **Hallucinated links**: AI may invent GitHub issue numbers or doc
   URLs. Constrain via MCP + post-validation (HTTP HEAD check on
   cited URLs).
6. **Customer surprise**: The customer who filed internally may not
   expect their report to appear on a public tracker.

### 9.2 Mitigations (mandatory)

- **Sanitize before AI**: Step 4 strips PII *before* sending content
  to the model. AI prompt explicitly says "do not request PII".
- **Reporter identity**: The original AzDO `System.CreatedBy`
  display name is **never** included in the public GitHub issue
  body. The mirrored issue is anonymous from the public's
  perspective; the AzDO link in the body lets internal maintainers
  find the original reporter.
- **Sanitize before mirroring**: The mirroring branch of Step 6
  runs a second sanitization pass + a CredScan check on the
  rendered GitHub body.
- **Security-bug detector**: Keyword + classifier check (e.g.,
  `CVE`, `RCE`, `vulnerability`, `escalation`, `auth bypass`, MSRC
  ticket patterns) routes the report to `security` and pages a
  maintainer privately — no public mirror.
- **Customer notification**: The AzDO comment posted on close
  always includes a "Reply 'do not publish' within 7 days if you'd
  prefer we keep this internal" clause.
- **Human approval gate**: The `apply` job uses a GitHub
  [environment](https://docs.github.com/en/actions/deployment/targeting-different-environments)
  with required reviewers (`StellaHuang95` plus one more
  maintainer). Nothing writes until a human clicks "Approve and
  deploy" on the generated triage summary.
- **Dry-run default**: First N runs are forced `dry_run: true`.
- **Audit trail**: Each apply step writes a structured log line
  (work item ID, verdict, GitHub URL, timestamp, model version)
  and uploads it as a workflow artifact (90 days). The PII-bearing
  `candidates.json` artifact is retained 7 days only.
- **PII never enters the verdict artifact**: `apply-outcomes.ps1`
  composes `run-summary.md` from work-item IDs + AI verdicts only.
- **CODEOWNERS coverage**: This repo's [CODEOWNERS](CODEOWNERS)
  already contains a single global
  `*  @microsoft/ptvs-codereviewers` line, so changes to
  `.github/workflows/azdo-triage.yml` and `Build/triage/**` are
  automatically reviewed. No new entry needed.

### 9.3 Compliance / legal sign-off

Before turning auto-close on, get explicit written approval from:

- the PTVS team lead,
- the DevDiv area-path owners, and
- Microsoft privacy/compliance.

---

## 10. Phased rollout

Three phases. PTVS is in maintenance mode and traffic on the area
path is low, so the multi-phase ladder we'd want for a high-traffic
product is overkill.

| Phase | Scope | Exit criteria |
|---|---|---|
| **0. Spike** | `query-azdo.ps1` + `fetch-context.ps1` + a small notebook that prints WIQL results and the per-run research context. Verify SP auth. **Inspect one example work item** (`GET .../wit/workitems/{id}?$expand=all`) to capture: (a) the exact REST field name behind "Duplicate Feedback Ticket ID" on the Overview tab; (b) the full set of `DC - Closed - *` state-value strings available on these work items so we know which one to use for the `answered` outcome (likely `DC - Closed - Other` or `DC - Closed - Fixed`); (c) whether all items in the area path are DC-bridged or whether some are internally-filed bugs that need a different close flow. Compliance sign-off in parallel. | Spike script lists last 7 days of WIs accurately; field/state names captured in `Build/triage/config.json`; compliance has signed off. |
| **1. Dry-run** | Full workflow with `dry_run: true`. Job 1 fires (since it only writes the report issue, which is low-risk); Job 2 produces a Markdown summary artifact ("here's what we would do") but doesn't write to AzDO or mirror. | ≥ 2 consecutive Mondays where reviewers say "the AI verdict is sensible" on every candidate. |
| **2. Live** | Drop `dry_run`. All outcomes (`answered` / mirror-to-GitHub / leave-alone) write through. Human approval gate via the `azdo-triage-approval` environment for each run. Maintainer can flip back to dry-run via `workflow_dispatch`. Job 2 can be turned off entirely by leaving the schedule but unchecking `run_triage` on each manual run. | Steady-state — no defined exit. Review the run summary weekly; switch back to dry-run if quality drops. |

Roll-back at any phase by setting `dry_run: true` via
`workflow_dispatch` or disabling the workflow file.

---

## 11. Open questions / decisions needed

Each question has my **proposed answer** based on external research
and the repo's existing conventions; treat them as defaults unless a
stakeholder pushes back.

1. **Auth path** — confirm SP + federated credential is acceptable
   to DevDiv security.  
   **Proposed**: Pursue SP + federated. PAT is the documented
   fallback. Begin the SP request in parallel with phase 0.

2. **GitHub App vs PAT** — preferred for the bridge writer?  
   **Proposed**: Ship v1 with a fine-grained PAT
   (`PTVS_BRIDGE_PAT`). Track replacing it with a GitHub App as a
   follow-up after phase 1.

3. **Maintainer assignment** — `StellaHuang95` vs rotation?  
   **Proposed**: Default the first assignee to `StellaHuang95`.
   ⚠️ **Known interaction with
   [issues.yml](.github/workflows/issues.yml)**: that workflow
   fires on `issues.opened` for *any* user — including the bridge
   PAT — and calls `addAssignees` (additive). The bridge-created
   issue would end up assigned to both `StellaHuang95` AND the
   next rotation winner, plus the rotation state on issue #7774
   advances. Fix `issues.yml` to skip issues carrying the
   `from-azdo` or `azdo-triage-report` label (cleanest):
   ```yaml
   if: github.repository == 'microsoft/PTVS'
       && github.event.action == 'opened'
       && !contains(github.event.issue.labels.*.name, 'from-azdo')
       && !contains(github.event.issue.labels.*.name, 'azdo-triage-report')
   ```
   Required before phase 1.

4. **Label set** — propose adding new labels.  
   **Proposed**: Create `from-azdo` and `azdo-triage-report` (both
   new). Skip `triaged-by-ai` as a label — that marker is an AzDO
   *tag* on the work item, not a GH label. AI-emitted label set is
   restricted to a whitelist in `Build/triage/config.json`.

5. **Area paths exact list** — exclude any siblings?  
   **Proposed**: `[System.AreaPath] UNDER 'DevDiv\\Python and AI Tools'`
   includes everything under that root. Re-verify against the live
   area-path tree in phase 0 and add explicit `NOT UNDER` exclusions
   for any subtree that turns out to be unrelated.

6. **Work item types** — include `Feedback`/`Issue`?  
   **Proposed**: v1 sticks with `Bug, Task, User Story`. Phase 0
   prints the type distribution under the area path; if `Feedback`
   accounts for >10% of customer-originated reports, add it before
   phase 1.

7. **Customer opt-out** — binding?  
   **Proposed**: Yes, binding. The follow-up "do not publish"
   detector is a post-v1 enhancement. v1's mitigation: every
   `move-to-github` comment includes the 7-day opt-out clause; if a
   maintainer sees a "do not publish" reply on AzDO, they manually
   close the GH issue and reopen the AzDO item.

8. **Cost budget** — OK with consumption?  
   **Proposed**: Inside `microsoft` enterprise's GitHub Models
   allotment. Set `MAX_CANDIDATES_PER_RUN = 25` as a hard ceiling.
   Copilot cloud-agent assignment is not automated (§12), so there
   is no premium-request line item for v1.

9. **Repo for the workflow** — in-repo vs out-of-repo?  
   **Proposed**: Stay in `microsoft/PTVS`. Transparency outweighs
   the small extra exposure risk because (a) the candidates artifact
   has 7-day retention, (b) the verdict/summary artifacts contain no
   PII, (c) prompts contain no internal docs.

10. **Idempotency of Job 1** — what if the same week's report runs
    twice (manual + scheduled)?  
    **Proposed**: `post-report-issue.ps1` checks for an existing
    issue with the same exact title before creating. If one exists,
    *update its body* in place rather than file a duplicate. The
    weekly title (`YYYY-MM-DD to YYYY-MM-DD (<N> open items)`) is
    deterministic for the same lookback window, so this is a clean
    natural key.

11. **Developer Community field/state names** — exact REST values?  
    **Proposed**: Work items under `DevDiv\Python and AI Tools\**`
    are Developer Community feedback tickets, so close-as-duplicate
    must use `System.State = "DC - Closed - Duplicated"` and
    populate the "Duplicate Feedback Ticket ID" field on the
    Overview tab with the new GitHub issue URL (this is what
    triggers DC's transfer-votes flow). The exact REST field name
    behind that Overview-tab label is not in public docs — it's
    likely `Microsoft.VSTS.Common.DuplicateFeedbackTicketId` or a
    DevDiv-custom field. Capture it in phase 0 by fetching one
    example item with `?$expand=all` and store in
    `Build/triage/config.json` under `azdo.duplicateFieldName`.
    Same for the `answered` close-state value (probably
    `DC - Closed - Other` or `DC - Closed - Fixed`). If any items
    in the area path turn out to NOT be DC-bridged, the apply
    script needs a fallback close path (state `Closed`, reason
    `Resolved`) — also determined in phase 0.

---

## 12. Out of scope (for v1)

- **Auto-assigning the Copilot cloud agent** to mirrored bug issues.
  Maintainers do this manually when worthwhile (§6.3 Step 7, §6.4).
- Bidirectional sync (a GitHub comment back to AzDO). Closing AzDO
  and linking is one-way; further conversation happens on GitHub.
- Triage of work items older than the lookback window.
- Re-opening previously-closed AzDO items on follow-up customer
  comments.
- Stack-rank / severity scoring (maintainers handle severity by
  hand; with low traffic this is cheap).
- Migration of attachments. The GitHub issue links back to AzDO for
  attachments rather than re-hosting them.
- Localization of AI-drafted responses (English only in v1).
- Replacing the existing
  [issues.yml](.github/workflows/issues.yml) rotation. The bridge
  sits next to that workflow; `issues.yml` continues to
  rotate-assign issues not filed by the bridge (after the
  `from-azdo` / `azdo-triage-report` skip in §11.3).

---

## 13. References

- WIQL syntax: https://learn.microsoft.com/en-us/azure/devops/boards/queries/wiql-syntax
- Query By WIQL REST: https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/wiql/query-by-wiql
- Get Work Item: https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/get-work-item
- Update Work Item: https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/work-items/update
- Add Comment: https://learn.microsoft.com/en-us/rest/api/azure/devops/wit/comments/add
- Service principals & MIs with AzDO: https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/service-principal-managed-identity
- AzDO resource AppId (`499b84ac-1321-427f-aa17-267ca6975798`): see `az account get-access-token --resource` in the same doc.
- `actions/ai-inference` (verified inputs `prompt-file`, `input`, `file_input`, `enable-github-mcp`, `github-mcp-toolsets`, `model`, `max-completion-tokens`, output `response-file`): https://github.com/actions/ai-inference
- `actions/ai-inference` `.prompt.yml` schema with `responseFormat: json_schema` + `jsonSchema:`: https://github.com/actions/ai-inference#readme
- `azure/login@v2` (verified inputs `client-id`, `tenant-id`, `audience`, `allow-no-subscriptions`): https://github.com/Azure/login
- GitHub Models prototyping: https://docs.github.com/en/github-models/use-github-models/prototyping-with-ai-models
- GitHub Models responsible use: https://docs.github.com/en/github-models/responsible-use-of-github-models
- GitHub Copilot cloud agent (assignee literal `copilot-swe-agent[bot]` for REST, `copilot-swe-agent` for GraphQL): https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/start-copilot-sessions
- Copilot cloud agent Azure Boards integration: https://docs.github.com/en/copilot/how-tos/use-copilot-agents/cloud-agent/integrate-cloud-agent-with-azure-boards
- GitHub Copilot CLI: https://docs.github.com/en/copilot/concepts/agents/about-copilot-cli
- Example AzDO work item that motivated this plan: https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2711586

---

## 14. Appendix: minimal "happy path" curl commands

```bash
# 0) Mint an AzDO-resource access token using the federated SP.
#    Prereq: `azure/login@v2` ran successfully, so `az` is authenticated.
AZDO_TOKEN=$(az account get-access-token \
  --resource 499b84ac-1321-427f-aa17-267ca6975798 \
  --query accessToken -o tsv)

# 1) Query: which work items were created in the last 7 days under our area paths?
curl -sS -X POST \
  "https://dev.azure.com/devdiv/DevDiv/_apis/wit/wiql?api-version=7.1" \
  -H "Authorization: Bearer $AZDO_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "query": "SELECT [System.Id] FROM workitems WHERE [System.TeamProject]=@project AND [System.AreaPath] UNDER \"DevDiv\\Python and AI Tools\" AND [System.CreatedDate] >= @Today - 7"
  }' | jq '.workItems[].id'

# 2) Fetch full details for those IDs (batch)
curl -sS -X POST \
  "https://dev.azure.com/devdiv/_apis/wit/workitemsbatch?api-version=7.1" \
  -H "Authorization: Bearer $AZDO_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"ids":[2711586],"$expand":"All"}'

# 3) JOB 1 happy path: file the weekly report issue.
TITLE="AzDO triage report 2026-05-06 to 2026-05-13 (8 open items)"
BODY=$(cat <<EOF
8 open work items created in the past 7 days under \`DevDiv\\Python and AI Tools\\**\`.

- [AzDO #2711586](https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2711586) — \`<sanitized title>\` — created 2026-05-08 — area: Python\\VS IDE
- [AzDO #2711587](https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2711587) — \`<sanitized title>\` — created 2026-05-09 — area: Python

---
_Generated by \`.github/workflows/azdo-triage.yml\` (run #1234). Triage pipeline: enabled. See run for details._
EOF
)
curl -sS -X POST \
  "https://api.github.com/repos/microsoft/PTVS/issues" \
  -H "Authorization: Bearer $PTVS_BRIDGE_PAT" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  -d "$(jq -n --arg title "$TITLE" --arg body "$BODY" \
        '{title: $title, body: $body, labels: ["azdo-triage-report"], assignees: ["StellaHuang95"]}')"

# 4) Read the current rev (needed for concurrency-safe PATCH below)
REV=$(curl -sS \
  "https://dev.azure.com/devdiv/DevDiv/_apis/wit/workitems/2711586?api-version=7.1&fields=System.Rev" \
  -H "Authorization: Bearer $AZDO_TOKEN" | jq -r '.fields["System.Rev"]')

# 5) Post the canned DC duplicate template as a comment.
#    Customers recognize this wording from other VS feedback they have filed.
GH_URL="https://github.com/microsoft/PTVS/issues/12345"
COMMENT=$(cat <<EOF
Thanks for taking the time to report this issue! It seems to us that it's describing the same thing as [another one](${GH_URL}). We've closed this one as a duplicate and transferred all votes from this ticket to the other one. Please follow along on the linked ticket to communicate with the team, see updates on status, and help provide any needed diagnostic info. For more information, see our [issue reporting guidelines](https://aka.ms/vsfeedbackguidelines).

Happy coding!
EOF
)
curl -sS -X POST \
  "https://dev.azure.com/devdiv/DevDiv/_apis/wit/workItems/2711586/comments?api-version=7.0-preview.3" \
  -H "Authorization: Bearer $AZDO_TOKEN" \
  -H "Content-Type: application/json" \
  -d "$(jq -n --arg t "$COMMENT" '{text: $t}')"

# 6) Close the AzDO work item as a Developer Community duplicate
#    (concurrency-safe; pass the rev we just read).
#
#    Replace <DuplicateFeedbackTicketIdField> with the actual REST
#    field name (verified in phase 0). It is the field shown as
#    "Duplicate Feedback Ticket ID" on the Overview tab.
curl -sS -X PATCH \
  "https://dev.azure.com/devdiv/DevDiv/_apis/wit/workitems/2711586?api-version=7.1" \
  -H "Authorization: Bearer $AZDO_TOKEN" \
  -H "Content-Type: application/json-patch+json" \
  -d "[
    {\"op\":\"test\",\"path\":\"/rev\",\"value\":$REV},
    {\"op\":\"add\",\"path\":\"/fields/System.State\",\"value\":\"DC - Closed - Duplicated\"},
    {\"op\":\"add\",\"path\":\"/fields/<DuplicateFeedbackTicketIdField>\",\"value\":\"${GH_URL}\"},
    {\"op\":\"add\",\"path\":\"/fields/System.Tags\",\"value\":\"triaged-by-ai; moved-to-github\"}
  ]"

# 7) Create a mirror issue in microsoft/PTVS (Job 2)
curl -sS -X POST \
  "https://api.github.com/repos/microsoft/PTVS/issues" \
  -H "Authorization: Bearer $PTVS_BRIDGE_PAT" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  -d '{
    "title": "[AzDO #2711586] <sanitized title>",
    "body":  "<sanitized markdown body, includes link back to AzDO>",
    "labels":["bug","needs repro","from-azdo"],
    "assignees":["StellaHuang95"]
  }'

# 8) (Manual only — NOT done by the bridge in v1; see §6.3 Step 7.)
#    For reference, this is the call a maintainer would script if they
#    decided to delegate a mirrored bug to Copilot cloud agent.
#    Note the `[bot]` suffix on the REST API assignee literal.
curl -sS -X POST \
  "https://api.github.com/repos/microsoft/PTVS/issues/12345/assignees" \
  -H "Authorization: Bearer $PTVS_BRIDGE_PAT" \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  -d '{
    "assignees": ["copilot-swe-agent[bot]"],
    "agent_assignment": {
      "target_repo": "microsoft/PTVS",
      "base_branch": "main",
      "custom_instructions": "Propose a minimal, targeted fix. Add a test if feasible."
    }
  }'
```

---
*End of plan.*
