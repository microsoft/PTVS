Here’s a combined, summarized version of **Python 3.14 Debugging Support in PTVS** with the roadmap and current status integrated for clarity (updated after enabling managed safe attach by default, and after adding interpreter-walk inference + timing instrumentation):

---

# Python 3.14 Debugging Support in PTVS

## Overview

PTVS provides full **Python 3.14 debugging support** across all modes:

* Managed-only (debugpy) – safe attach now DEFAULT (opt‑out env var)
* Mixed-mode / native Concord engine (safe attach implemented Phase 1)
* Remote & safe attach (PEP 768) groundwork
* Stepping, breakpoints, evaluation
* Version recognition and backward compatibility (3.5–3.13)

Key change: Leverage CPython 3.14 `_Py_DebugOffsets` (`debugger_support`) for **safe attach** instead of legacy DLL/code injection. Managed path now attempts safe attach first and only falls back to legacy injection if it fails or is explicitly disabled (`PTVS_SAFE_ATTACH_MANAGED_DISABLE=1`).

---

## Refactor Progress Snapshot
| Step | Description | Status |
|------|-------------|--------|
| 1 | Introduce `SafeAttachConfig` (env snapshot) | DONE (in use) |
| 2 | Extract `ThreadStateValidator` | DONE (centralized candidate validation) |
| 3 | Move walk inference + traversal to `InterpreterWalkLocator` | DONE (orchestrator slimmed) |
| 4 | `ProcessMemory` helper | DONE (integrated in orchestrator) |
| 5 | `RemoteWritePlan` | Pending |
| 6 | `PhaseTimer` (may stay dropped if timing not needed) | Deferred / reconsider |
| 7 | Unified logging facade | Pending |
| 8 | State machine orchestration | Pending |
| 9 | Unit tests for components | Partial (config + validator + locator; add ProcessMemory & write plan tests) |
| 10 | Telemetry scaffolding | Pending |

Recent Refactor Updates:
* Validator & walk locator extracted; orchestrator sequence: force → cache → infer (if needed) → walk → heuristic (opt‑in) → export (opt‑in).
* Inference + walk isolated for future Concord reuse.
* `ProcessMemory` helper added; orchestrator now uses it for slab read, eval breaker read, script buffer probe.

---

## Key Insights (What Made It Work)

* Stop assuming interpreter-walk sextuple is adjacent to the 5‑tuple; perform slab‑wide scan.
* Treat all `_Py_DebugOffsets` entries as offsets (not absolute pointers) – compose addresses at runtime.
* Validate interpreter-walk candidates against live memory (runtime → interpreter → thread list) before use.
* Expand DLL regex to match two or three digit minor versions: `python310/311/314.dll`.
* Deterministic resolution order: cache → inferred/parsed walk → (optional) heuristic → (optional) export fallback.
* Strengthen `PyThreadState` validation: probe eval_breaker (4 bytes), pending flag (int32), script path buffer readability.
* Use fixed 32‑bit eval breaker bit mask 0x20 for 3.14+; retain dynamic mask logic only for <3.14.
* Bypass export/TLS path by default for 3.14+ (reduce dependency & surface area).
* Early exit on first validated `tstate`; count candidates for diagnostics.
* Gate heuristic scan behind explicit env var; avoid unnecessary slow speculative scans.
* Revalidate cached `tstate` cheaply (offset-based field probes) before reuse.
* Ensure UTF‑8 loader script buffer is NUL‑terminated and within declared size.
* Add pending flag read probe to eliminate false positives in corrupted / stale memory cases.

---

## Code Simplification Plan (Upcoming Refactors)
Goal: reduce complexity / duplication in `SafeAttachOrchestrator` while preserving diagnostics.

| Area | Current Issue | Planned Simplification | Benefit |
|------|---------------|------------------------|---------|
| Env Var Lookups | Scattered repeated `EnvVarTrue` calls | Centralize into immutable `Config` snapshot object constructed once per attempt | Fewer branches, clearer intent |
| Validation Logic | Mixed inside orchestrator | Extract `ThreadStateValidator.Validate` enum result | Readability & test coverage |
| Walk Inference & Walk | Inline private methods | `InterpreterWalkLocator.Infer` / `Find` | Separation of concerns |
| Pointer Reads | Repeated lambdas | `ProcessMemory` helper | Less boilerplate |
| Write Sequence | Inline procedural | `RemoteWritePlan.Execute` | Easier post-write verification |
| Error Construction | Many early returns | Result discriminator | Cleaner control flow |
| Heuristic Gate | Inline conditions | Strategy chain | Extensibility |
| Test Helpers | Blob builders inline | `OffsetsBlobBuilder` | Reusable fixtures |
| Magic Constants | Literals inline | `SafeAttachConstants` | Self-documenting |

(Other sections unchanged below.)
