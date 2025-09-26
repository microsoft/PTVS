Here’s a combined, summarized version of **Python 3.14 Debugging Support in PTVS** with the roadmap and current status integrated for clarity:

---

# Python 3.14 Debugging Support in PTVS

## Overview

PTVS is adding full **Python 3.14 debugging support** across all modes:

* Managed-only (via debugpy) **(Safe Attach rollout in progress)**
* Mixed-mode / native Concord engine (safe attach implemented Phase 1)
* Remote & safe attach (PEP 768)
* Stepping, breakpoints, evaluation
* Version recognition and backward compatibility (3.5–3.13)

Key change: Leverage CPython 3.14's `debugger_support` structure for **safe attach** instead of legacy code injection. Managed-only path is being upgraded to prefer safe attach (feature-flagged) before falling back to DLL injection.

---

## Goals

* Recognize Python 3.14 everywhere version gating exists.
* Implement safe remote attach using PEP 768 facilities.
* Update mixed-mode/native data model for 3.14 runtime changes.
* Add managed-only (debugpy) safe attach path (Phase 2) with clean fallback to legacy injection.
* Preserve existing behavior for ≤3.13.
* Upgrade debugpy to 1.9.0 (done).
* Add comprehensive test coverage for launch, attach, stepping, evaluation.

Out of Scope: IntelliSense updates, profiling, or REPL changes beyond 3.14 version acceptance.

---

## High-Level Work Items

1. **Version Recognition & Constants** – Add `V314` enum, update predicates, paths, resources.
2. **debugpy Upgrade** – Bump to 1.9.0 (completed).
3. **_Py_DebugOffsets Extensions** – Parse new 3.14 fields: `eval_breaker`, `pending_call`, `script_path`, etc.
4. **Safe Attach Implementation (Native/Mixed)** – (Completed Phase 1) Memory writes + fallback chain.
5. **Safe Attach Implementation (Managed-only)** – Feature-flagged; add offsets reader & write sequence; fallback to legacy injection.
6. **Stepping Reliability** – Adjust for potential frame/code object changes.
7. **Resilience/Fallback** – Fail gracefully, revert to legacy if needed.
8. **Telemetry & Strings** – Safe attach success/failure codes, loader reuse flags, managed path events.
9. **Docs & Samples** – Update mixed-mode/remote & managed-only attach guides.
10. **Test Matrix** – Automated + manual validation across modes and versions.
11. **Final Validation** – Backward compatibility, performance baseline checks.

---

## Current State (as of 2025-09-24 / updated progress)

* **Safe Attach Protocol Implemented (Native/Mixed)**
  * Memory writes for script path + pending flag + eval breaker stop bit.
  * Fallback chain for thread state discovery: main → enumerate → export → heuristic.
  * Loader reuse: existing `ptvsd_loader.py` invoked via safe attach for now; minimal loader planned later.
  * Telemetry logs selection method, attach result, truncation, reattach optimization.

* **Managed-only Safe Attach – Phase 2 Progress**
  * Phase 2A scaffold merged: feature flag `PTVS_SAFE_ATTACH_MANAGED=1` gates attempt.
  * Shared parser extracted (`DebugOffsetsParser`) – reused by Concord and managed path.
  * Shared safe attach abstractions (`SafeAttachCommon`: result / failure enums) added.
  * Managed orchestrator skeleton (`SafeAttachOrchestrator`) created (currently version detection only, always falls back).
  * Next: implement offsets location + parse + (later) memory write sequence.

* **Shared Code Convergence**
  * Offsets parsing now single implementation; Concord reader invokes shared parser.
  * Plan to add shared thread state locator & memory writer modules (Phase 2B+).

* **Legacy Compatibility Confirmed**
  * Attach scripts for ≤3.13 remain functional.
  * Safe attach is version-gated and policy-aware (`RemoteDebugDisabled`).

* **Validation**
  * Native path tests in place (offsets + fallbacks); managed path tests pending.
  * Performance tests for attach latency pending.

---

## Managed-only Safe Attach Rollout Plan

### Phase 2A (Scaffolding – DONE)
* Feature flag & gating.
* Orchestrator skeleton with version detection.
* Shared parser + result enums.
* Always falls back (no behavior change).

### Phase 2B (Core Functionality – IN PROGRESS)
* Offsets address discovery for managed path (symbol / section / scan fallback).
* Parse offsets via shared parser.
* Minimal thread state discovery (export `_PyThreadState_UncheckedGet` or heuristic scan).
* Implement memory writes (script path, pending flag, eval breaker stop bit) – behind sub-flag if needed.
* Telemetry event `ManagedSafeAttachAttempt` (success / failureSite / version / flags / timings).

### Phase 2C (Robustness & Optimization)
* Reattach cache (PyThreadState reuse validation).
* Partial-write fault injection + truncation tests.
* Dynamic eval_breaker mask derivation (RT1).
* Output window diagnostics for failure taxonomy.

### Phase 2D (Enable by Default)
* Flip semantics: enabled unless opt-out env var set.
* Monitor success & fallback metrics, attach latency (P50/P95 delta vs legacy).

### Phase 2E (Minimal Loader)
* Replace `ptvsd_loader.py` for safe path with slim bootstrap (reduce buffer footprint & startup latency).

---

## Design Decisions & Risks

* **Atomicity**: Minimal writes, no process suspension for now.
* **Safety**: Structured parsing + bounds checks before writes.
* **Fallbacks**: Legacy path always available.
* **Shared Logic**: Parser & result types centralized to reduce divergence.
* **Thread State Risk**: Heuristic false positives mitigated via pointer plausibility + revalidation.
* **Free-Threaded Flag**: Observed in shared parser; future handling TBD for nogil builds.

---

## Telemetry & Logging

* **AttachAttempt / ManagedSafeAttachAttempt**: result, failureSite, versionHex, flags, truncation, reuse, elapsedMs.
* **Offsets**: pointer values & size sanity (debug-level logging only).
* **Failure Taxonomy**: openProcess, offsetsResolve, parse, policyDisabled, threadState, writeScript, writeBreaker, timeoutConnect.

---

## Test Matrix (Incremental Additions)

Managed path additions to implement during 2B–2C:
* Offsets parse success / failure.
* Policy disabled path.
* Truncated script path.
* Partial write failure (simulated via injected failing WriteProcessMemory wrapper).
* Reattach with cached thread state.

---

## Next Actions (Updated)

1. **MA1**: Managed offsets address discovery + parse (implement in orchestrator).
2. **MA2**: Thread state locator (export-based + fallback heuristics).
3. **MA3**: Memory write logic & success path (stop bit + pending flag + script path).
4. **TM1**: Telemetry enrichment & wiring for managed attempts.
5. **PW1**: Partial write failure simulation framework.
6. **CA1**: Thread state cache + validation.
7. **RT1**: Eval breaker dynamic mask derivation.
8. **TH1**: Optional full interpreter/thread enumeration telemetry.
9. **MA4**: Enable-by-default readiness criteria + guard flag flip.
10. **ML1**: Minimal loader implementation & integration.

---

## Legacy vs. 3.14 Safe Attach Comparison

| Aspect           | ≤3.13 Legacy            | 3.14 Safe Attach (PEP 768)     | Benefit               |
| ---------------- | ----------------------- | ------------------------------ | --------------------- |
| Discovery        | Heuristic symbol lookup | Structured `_Py_DebugOffsets`  | Lower fragility       |
| Entry            | Code injection calls    | Script path + flags + stop bit | Reduced crash surface |
| Thread State     | Current thread only     | Ordered fallback chain         | Higher success rate   |
| Validation       | Minimal                 | Size, bounds, partial checks   | Safety                |
| Reattach         | Full re-run             | Cached `PyThreadState` reuse   | Performance           |
| Policy Awareness | Limited                 | `RemoteDebugDisabled` flag     | Governance            |
| Telemetry        | Sparse                  | Structured, detailed           | Diagnostics           |
| Compatibility    | Single path             | Legacy + Safe Attach coexist   | Smooth migration      |

---

## Done Definition (Phase 1)
* Native safe attach implemented & validated.

## Done Definition (Phase 2A – Managed Scaffolding)
* Shared parser + enums.
* Managed orchestrator skeleton.
* Feature flag gating; always fallback.
* No regressions.

---

(Visual roadmap diagram to be added after Phase 2B implementation.)
