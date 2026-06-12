<!--
    Prompt template for PTVS-Triage-Analyze (loaded by Build/triage/analyze.ps1).

    PLACEHOLDERS (filled in by analyze.ps1 before sending to Copilot CLI):
      {WI_ID}     numeric AzDO work-item id
      {WI_URL}    canonical AzDO web URL
      {WI_JSON}   the sanitized wi-<id>.json content, embedded as a fenced JSON block
      {DIAG_JSON} the sanitized diag-<id>.json content, embedded as a fenced JSON block

    DESIGN NOTES:
      - The two JSON blobs are the EXIT of the Fetch pipeline's sanitize.ps1, which
        already redacted PII / dropped abort-class secrets. analyze.ps1 additionally
        refuses to run on any wi-*.json whose `sanitization_aborted` field is true,
        so this prompt never sees content the sanitizer flagged.
      - The output section is mandatory. The first line MUST be the marker comment
        `<!-- ptvs-triage-analyze:{WI_ID} -->` so post-analyses.ps1 can detect
        idempotency (skip-if-already-posted) by string-matching it in existing
        comments on the triage issue.
      - The model has no internet beyond what Copilot CLI's tools allow.
        It can `gh issue list` / `gh pr list` / `gh issue view` against
        microsoft/PTVS (the pipeline plumbs in $GH_TOKEN for `gh`), but
        it cannot fetch arbitrary URLs.
-->

You are triaging a Visual Studio Python Tools (PTVS) Developer Community feedback
ticket. The microsoft/PTVS source tree is checked out at your current working
directory. Read files with the tools you have (grep, file view, etc.).

## Work item (sanitized)

```json
{WI_JSON}
```

## Diagnostics (sanitized)

This may include attachment excerpts and, when a `PythonToolsDiagnostics_*.log`
was attached to the original work item, a structured `python_tools_diagnostics`
block with VS / PTVS / Python versions, loaded assemblies, and last exceptions.

```json
{DIAG_JSON}
```

## Your task

1. **Classify** the most likely root cause as exactly ONE of:
   - `PTVS` — likely in microsoft/PTVS code paths
   - `Upstream` — Visual Studio platform, Pylance, debugpy, language server, or
                  another dependency outside this repo
   - `Pure Python` — a user / library / environment problem unrelated to PTVS
   - `Unable to determine` — insufficient information to classify

2. If category is `PTVS` or `Upstream`, **cite specific files** you actually opened.
   Use the form `path/to/file.cs:L120-L145` so a reviewer can click through. Do not
   fabricate file paths. If you opened a file and found nothing relevant, say so.

3. **Search this repo's existing GitHub issues and PRs** for duplicates or related
   fixes using the `gh` CLI. Examples:
       gh issue list --repo microsoft/PTVS --search "<keywords>" --state all --limit 5
       gh pr list    --repo microsoft/PTVS --search "<keywords>" --state all --limit 5
   Surface up to 3 most relevant items; skip the section if none are clearly related.

4. **Draft a customer-friendly response** that a PTVS maintainer can copy
   into Developer Community (the maintainer posts it; you do not). Tone:
   friendly, factual, acknowledge their report, set expectations honestly.
   Do not promise fixes or timelines. The CUSTOMER RESPONSE section must
   not contain any internal-only language (AzDO IDs, internal team names,
   etc.); the surrounding analysis sections for the maintainer's eyes can
   reference AzDO IDs and internal context freely.

## Output

Produce ONLY the following markdown, in this exact order and structure. No
preamble, no closing chatter — your entire stdout will be posted verbatim as a
comment on a public GitHub issue.

```
<!-- ptvs-triage-analyze:{WI_ID} -->
### Triage analysis for [AzDO #{WI_ID}]({WI_URL})

**Root cause category:** <PTVS | Upstream | Pure Python | Unable to determine>
**Confidence:** <Low | Medium | High>

**Diagnosis**

<2–4 short paragraphs. Reference file:line evidence where applicable.>

**Evidence from repo**

- `path/to/file.ext:L<start>-L<end>` — <what you observed there>
- ... (omit the section entirely if you opened no files)

**Related issues / PRs**

- #<num> — <one-line why related>
- ... (omit the section entirely if none are clearly related)

**Suggested customer response**

> <draft reply, friendly, ≤ 200 words; quote-block so it's visually
>  distinct from analysis>

**Recommended maintainer action:** <Close as duplicate of #N | Request more info from customer | Investigate further | File spin-off bug | Out of scope / not actionable>
```

Word budget: keep the whole output under ~1500 words. Brevity beats padding.
