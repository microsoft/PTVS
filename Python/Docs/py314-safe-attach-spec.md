Here’s a combined, summarized version of **Python 3.14 Debugging Support in PTVS** with the roadmap and current status integrated for clarity (updated after enabling managed safe attach by default):

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

## NEW (RF1 Progress) – Shared Safe Attach Components Added

The refactor (RF1) now includes a growing set of shared helpers under `Product\Common\Debugger\SafeAttach` used by both the managed Attacher and Concord (native) path:

| File | Purpose | Used By |
|------|---------|---------|
| `PeExportReader.cs` | Parses & caches PE export table (name→RVA, lightweight function/data heuristic). | Managed, Concord |
| `ThreadStateExportResolver.cs` | Resolves current `PyThreadState*` via `_PyThreadState_Current` (data) or `_PyThreadState_UncheckedGet` (pattern scan). | Managed, Concord |
| `SafeAttachUtilities.cs` | Loader path UTF8 buffer creation + truncation logic. | Managed, Concord |
| `EvalBreakerHelper.cs` | Unified stop-bit mask determination (dynamic-existing bit, heuristic candidates, default) + metadata. | Managed, Concord (replaces legacy dynamic mask logic) |
| `ThreadStateCacheValidator.cs` | Lightweight validation of cached tstate before reuse (remote support + script path probe). | Managed (now), planned Concord reuse |
| `HeuristicThreadStateLocator.cs` | Heuristic backward scan from `eval_breaker` to derive plausible PyThreadState base (optional). | Managed (env opt-in), Concord (will migrate) |

### Refactored Callers
* `SafeAttachOrchestrator` now uses: export resolver, loader util, unified stop-bit helper, cache validator, optional heuristic locator (gated by `PTVS_SAFE_ATTACH_MANAGED_HEURISTIC`).
* `RemoteAttach314` uses: export resolver, loader util, unified stop-bit helper. (Heuristic path still local; planned migration to shared locator.)

### Immediate Benefits
* Elimination of duplicate PE parsing, export scanning, and stop-bit logic.
* Consistent stop-bit mask selection (with source classification) across managed & Concord paths.
* Shared cache validation reduces false reuse & rediscovery churn.
* Optional heuristic locator isolated and testable; can be enabled selectively.

### Remaining Commonization Opportunities (Planned)
1. Integrate Concord heuristic scan with `HeuristicThreadStateLocator` (remove local implementation).
2. Use `ThreadStateCacheValidator` in Concord prior to attempting rediscovery.
3. Centralize write verification (script/pending/breaker) into a shared helper returning a structured outcome.
4. Introduce `ThreadStateLocator` strategy abstraction (composition of: cache → main interpreter (Concord only) → enumeration → export → heuristic).
5. Shared telemetry event producer (Attempt/Result) consuming all helper metadata (mask source, resolution path, cache reuse, truncation, heuristic usage).
6. Fault injection seam (partial write simulation) moved from Concord local flag into shared configurable delegate.

### Completed (New Since Last Update)
* Unified stop-bit mask logic (`EvalBreakerHelper`) – removed legacy dynamic derivation in Concord.
* Added shared heuristic & cache validation helpers and integrated into managed path.

### Not in Scope (Now)
* Symbol-based (DIA) fallback for exports.
* Cross-process enumeration unification (Concord retains richer proxy-based selection logic for now).

---

## Goals

* Recognize Python 3.14 everywhere version gating exists. (DONE)
* Implement safe remote attach using PEP 768 facilities. (IN PROGRESS – local attach done)
* Update mixed-mode/native data model for 3.14 runtime changes. (DONE Phase 1)
* Managed-only safe attach path with fallback. (CORE COMPLETE; enabled by default)
* Preserve existing behavior for ≤3.13. (DONE)
* Upgrade debugpy to 1.9.0. (DONE)
* Comprehensive test coverage (attach, launch, stepping, evaluation). (PARTIAL – expanding with new helpers)
* Telemetry instrumentation + reliability metrics. (PENDING)
* Refactor thread-state discovery to shared export parsing helper (RF1 PARTIAL – multiple helpers now integrated)

Out of Scope: IntelliSense, profiling, or REPL behavioral changes beyond version acceptance.

---

## High-Level Work Items (Status)

1. Version Recognition & Constants – DONE
2. debugpy Upgrade – DONE
3. `_Py_DebugOffsets` Parsing – DONE (shared parser)
4. Native/Mixed Safe Attach – DONE (Phase 1)
5. Managed Safe Attach – DEFAULT (cache, stop bit heuristic → unified; opt‑out flag)
6. Stepping Reliability Adjustments – Monitoring
7. Resilience / Fallback – Active
8. Telemetry & Strings – PENDING
9. Docs & Samples – IN PROGRESS
10. Test Matrix – EXPANDING
11. Refactor export parsing / thread-state lookup (RF1) – PARTIAL (core + cache + stop-bit + heuristic + validation integrated)
12. Final Validation / Performance Baseline – PENDING

---

## Current State (Updated)

* **Safe Attach (Native/Mixed)**: Stable; export & stop-bit logic unified.
* **Managed Safe Attach**: Export + heuristic (optional) + cache validation + unified stop-bit logic.
* **Shared Layer**: Consolidated most low-level primitives; remaining local logic in Concord: multi-tier selection + current heuristic scan (to migrate).
* **Telemetry**: Still debug logging only; helpers now surface metadata ready for event schema.

---

## Thread State Discovery Refactor Plan (RF1)
**Completed:** Export resolver, loader util, unified stop-bit, cache validator, heuristic locator (managed integration), orchestrator simplification.
**Pending:** Concord adoption of shared heuristic & validator, consolidated locator strategy, telemetry instrumentation.

---

## Telemetry & Logging (Planned vs Current)

| Aspect | Current | Planned |
|--------|---------|---------|
| Event Emission | Debug.WriteLine | Structured Attempt/Result events |
| Failure Taxonomy | Enum only | Event fields + aggregation |
| Timing | Total elapsed | Phase breakdown (resolve/cache/heuristic/write) |
| Cache Reuse | Log message | Flag + age (ms) |
| Truncation | Log message | Field (size, truncatedLength) |
| Stop-Bit Mask | Logged mask + source | Field + derived source enum |
| Export Resolution Source | Implicit | Field (data/function/heuristic/cache) |
| Heuristic Usage | Implicit | Field (used, scanRangePages) |

---

## Next Actions (Updated)
1. **TM1**: Telemetry event schema + wiring (include maskSource, resolutionPath, cacheReused, heuristicUsed).
2. **RF1-Continued**: Move Concord heuristic to `HeuristicThreadStateLocator`; adopt `ThreadStateCacheValidator`.
3. **RF1-Strategy**: Introduce `ThreadStateLocator` orchestrating cache → main → enum → export → heuristic (configurable).
4. **PW1**: Fault injection seam unify (partial write simulation into shared helper) + tests.
5. **CA1**: Extended validation (sanity of pending flag & script buffer content after write, pointer bounds telemetry).
6. **MA-Tests**: Add unit tests targeting each shared helper (export resolver, stop-bit helper, heuristic locator, cache validator) and orchestrator integration scenarios.
7. **DX1**: Summarized single-line outcome in Output window (first attempt per process).
8. **ML1**: Minimal loader script reduction (smaller UTF8 footprint).
9. **EN1**: Baseline attach latency & success metrics once telemetry live.

---

## Legacy vs. 3.14 Safe Attach Comparison

| Aspect | ≤3.13 Legacy | 3.14 Safe Attach | Benefit |
|--------|--------------|------------------|---------|
| Discovery | Heuristic symbol lookup | `_Py_DebugOffsets` + shared resolver + (optional) heuristic | Robust & extensible |
| Entry Mechanism | Remote thread + DLL injection | Script path + flags + stop bit | Lower crash surface |
| Thread State | Current thread only | Export + cache + heuristic fallback | Higher success rate |
| Validation | Minimal | Structured parse + cache validation | Safety |
| Reattach | Full rediscovery | Cache reuse (validated) | Performance |
| Policy Awareness | Limited | Honors `RemoteDebugDisabled` | Governance |
| Stop-Bit Selection | Fixed constant | Unified dynamic/heuristic helper | Future-proof |
| Telemetry | Sparse | (Planned) Rich events | Diagnostics |
| Code Duplication | High | Shared helpers (RF1) | Maintainability |

---

(Other sections unchanged below – test plan already reflects new helpers; will expand telemetry cases after TM1.)
