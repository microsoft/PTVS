# Triage handoff schema

This file documents the JSON contract between the two halves of the simplified
triage workflow (introduced 2026-06):

  1. **AzDO Pipeline 1** (`Build/triage/pipelines/fetch.yml`, in
     devdiv/DevDiv) — fetches AzDO work items, sanitizes them, parses
     diagnostic attachments, assembles `input.json`, and commits it to
     the private companion repo `microsoft/PTVS-triage-data` under
     `runs/<correlation_id>/input.json`.

  2. **GitHub Actions workflow** (`.github/workflows/triage-draft.yml`,
     in microsoft/PTVS) — reads `input.json`, drafts one suggested
     customer response per work item via `actions/ai-inference@v1`,
     aggregates the drafts into `drafts.json`, and posts a single weekly
     summary issue with the drafts inline.

The two systems do not call each other's APIs directly (that's the whole
reason this architecture exists — DevDiv blocks GitHub-hosted runner IPs
from authenticating to AzDO). The companion repo is the only handoff
channel; the `correlation_id` is the trust anchor verified on both sides.

## `runs/<correlation_id>/input.json`

```json
{
  "schema_version": "1",
  "correlation_id": "ado-12345-2026-06-04T08-00-00Z",
  "source_azdo_build_id": 12345,
  "source_azdo_build_url": "https://dev.azure.com/devdiv/DevDiv/_build/results?buildId=12345",
  "generated_at": "2026-06-04T08:00:00Z",
  "lookback_days": 7,
  "area_root": "DevDiv\\Python and AI Tools",
  "candidates": [
    {
      "id": 2711586,
      "rev": 3,
      "work_item_type": "Bug",
      "state": "Active",
      "area_path": "DevDiv\\Python and AI Tools\\Python\\VS IDE",
      "tags": "vsfeedback",
      "url": "https://dev.azure.com/devdiv/DevDiv/_workitems/edit/2711586",
      "created_date": "2026-05-08T12:00:00Z",
      "comment_count": 0,
      "title": "Visual Studio crashes when I open a Python project",
      "description_html": "<p>...</p>",
      "repro_steps_html": "<ol>...</ol>",
      "system_info": "<p>Windows 11, VS 17.10.1, Python 3.11.4</p>",
      "comments": [],
      "attachments": [
        { "name": "PythonToolsDiagnostics_*.log", "url": "..." }
      ],
      "sanitization_aborted": false,
      "secret_hits": []
    }
  ],
  "diagnostics": {
    "2711586": {
      "vs_version": "17.10.1",
      "ptvs_version": "17.10.1234",
      "python_version": "3.11.4",
      "debugger_type": "ptvsd",
      "machine_os": "Windows 11",
      "loaded_assemblies": ["..."],
      "last_exceptions": ["..."],
      "source_attachment_name": "PythonToolsDiagnostics_20260508.log",
      "source_attachment_url": "https://dev.azure.com/devdiv/..."
    }
  }
}
```

### Field semantics

| Field | Required | Meaning |
|---|---|---|
| `schema_version` | yes | Currently `"1"`. Bump if breaking changes. |
| `correlation_id` | yes | Trust anchor. Format: `ado-<build_id>-<rfc3339_z>` with `:` → `-`. AzDO Pipeline 1 mints; GH workflow verifies. |
| `source_azdo_build_id` | yes | The AzDO Pipeline 1 run's `$(Build.BuildId)`. For audit. |
| `source_azdo_build_url` | yes | Full URL to that run. For audit. |
| `generated_at` | yes | RFC 3339 UTC timestamp of when input.json was assembled. |
| `lookback_days` | yes | How many days back the WIQL query scanned. Echoed in the weekly issue title. |
| `area_root` | yes | Area path the WIQL query was rooted at. Echoed in the weekly issue body. |
| `candidates[]` | yes | Sanitized work items. Shape matches existing `wi-<id>.json` (see `Build/triage/sanitize.ps1`). Order: AzDO-default by ChangedDate desc. |
| `diagnostics{}` | yes (may be `{}`) | Map from work-item-id string to parsed diagnostic header. Shape matches existing `diag-<id>.json`. A candidate without diagnostics has no entry. |

## `runs/<correlation_id>/drafts.json`

Phase 1 does NOT push this file back to the companion repo (drafts only go
into the GitHub issue body). Phase 3 will add archival of `drafts.json` so
later searches can find prior drafts.

```json
{
  "schema_version": "1",
  "correlation_id": "ado-12345-2026-06-04T08-00-00Z",
  "github_run_id": 26986259106,
  "github_run_url": "https://github.com/microsoft/PTVS/actions/runs/26986259106",
  "generated_at": "2026-06-04T08:30:00Z",
  "drafts": [
    {
      "work_item_id": 2711586,
      "drafted_response_md": "Thanks for reporting this! ...",
      "model": "openai/gpt-4o",
      "prompt_version": "1"
    }
  ]
}
```

### Field semantics

| Field | Required | Meaning |
|---|---|---|
| `schema_version` | yes | Currently `"1"`. |
| `correlation_id` | yes | MUST match the `input.json` this draft was generated from. The aggregation step in `triage-draft.yml` enforces this. |
| `github_run_id` | yes | `${{ github.run_id }}` of the workflow that produced the drafts. |
| `github_run_url` | yes | Full URL to that GitHub Actions run. |
| `generated_at` | yes | RFC 3339 UTC. |
| `drafts[]` | yes | One per work item. Order: same as `input.json.candidates[]`. |
| `drafts[].work_item_id` | yes | AzDO work item ID. Cross-references `candidates[].id`. |
| `drafts[].drafted_response_md` | yes | The model's suggested customer-facing response, in markdown. Reviewed by humans before they paste into AzDO. Empty string allowed when the model declined to draft (`needs_info` case). If the value starts with the sentinel `SECURITY_HOLD`, the aggregation step in `triage-draft.yml` drops that draft (and the corresponding candidate bullet) from the public weekly issue entirely. |
| `drafts[].model` | yes | Model identifier from `actions/ai-inference` (e.g., `openai/gpt-4o`). For audit. |
| `drafts[].prompt_version` | yes | Version string of the system prompt that produced the draft. Bump in `triage-draft.yml` if the prompt changes meaningfully. |

> **Note on correlation_id enforcement.** The `prepare` job in `triage-draft.yml`
> verifies that the `correlation_id` inside `input.json` matches the workflow
> dispatch input before any matrix jobs run. Phase 1 does NOT re-verify on
> the aggregation/posting side because those steps consume only artifacts
> produced within the same workflow run. If you ever decouple aggregation
> from the same run (e.g. moving it to a follow-up workflow), re-verify the
> correlation_id at that boundary.

## Concurrency & retention

- The companion repo `microsoft/PTVS-triage-data` uses **immutable per-run paths**
  (`runs/<correlation_id>/...`). Two concurrent runs cannot clobber each other.
- AzDO Pipeline 1 must `git pull --rebase` before pushing; never force-push.
- A cleanup job (Phase 3, deferred) deletes `runs/<id>/` paths older than 90 days.

## Phase 1 fallback: local fixtures

The GH workflow's `fixtures_path` input bypasses the companion-repo checkout
entirely and reads `input.json` from a path in the PTVS checkout. Use this
before the companion repo and AzDO pipeline exist:

```
gh workflow run triage-draft.yml --ref <branch> \
  -f fixtures_path=Build/triage/tests/fixtures/input-sample.json \
  -f correlation_id=local-test-001
```

Phase 1 ships with `Build/triage/tests/fixtures/input-sample.json` matching
this schema so the workflow is end-to-end testable without any external
setup.
