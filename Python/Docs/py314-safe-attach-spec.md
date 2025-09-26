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

Key change: Leverage CPython 3.14's `debugger_support` structure for **safe attach** instead of legacy code injection. Managed-only path now has functional safe attach (feature-flag gated) with fallback to DLL injection.

---

## Goals

* Recognize Python 3.14 everywhere version gating exists.
* Implement safe remote attach using PEP 768 facilities.
* Update mixed-mode/native data model for 3.14 runtime changes.
* Add managed-only (debugpy) safe attach path with clean fallback to legacy injection.
* Preserve existing behavior for ≤3.13.
* Upgrade debugpy to 1.9.0 (done).
* Add comprehensive test coverage for launch, attach, stepping, evaluation.

Out of Scope: IntelliSense updates, profiling, or REPL changes beyond 3.14 version acceptance.

---

## High-Level Work Items

1. **Version Recognition & Constants** – Add `V314` enum, update predicates, paths, resources.
2. **debugpy Upgrade** – Bump to 1.9.0 (completed).
3. **_Py_DebugOffsets Extensions** – Parse new 3.14 fields: `eval_breaker`, `pending_call`, `script_path`, etc. (shared parser complete)
4. **Safe Attach Implementation (Native/Mixed)** – (Completed Phase 1) Memory writes + fallback chain.
5. **Safe Attach Implementation (Managed-only)** – Core functionality implemented behind flags (offsets parse, thread state, writes, stop bit).
6. **Stepping Reliability** – Pending validation on managed safe attach code path.
7. **Resilience/Fallback** – Legacy fallback path maintained.
8. **Telemetry & Strings** – Managed telemetry wiring pending (debug log only so far).
9. **Docs & Samples** – Updates in progress.
10. **Test Matrix** – Managed path tests to be added (Phase 2C).
11. **Final Validation** – Pending performance + regression verification.

---

## Current State (Updated)

* **Safe Attach Protocol Implemented (Native/Mixed)** – COMPLETE (Phase 1)

* **Managed-only Safe Attach – Implemented (Phase 2B/early 2C)**
  * Offsets discovery: PE section + fallback byte scan.
  * Shared parser integration (`DebugOffsetsParser`).
  * Thread state discovery via export `_PyThreadState_Current` with reuse cache.
  * Memory writes: script path (UTF-8 + null, truncation handling), pending flag (byte=1), eval breaker stop-bit OR (dynamic heuristic mask).
  * Reattach cache with validation (can disable via `PTVS_SAFE_ATTACH_MANAGED_NO_CACHE=1`).
  * Dynamic (heuristic) stop-bit mask selection among candidate bits (RT1 refinement still pending).
  * Feature flags:
    * `PTVS_SAFE_ATTACH_MANAGED=1` – enable attempt.
    * `PTVS_SAFE_ATTACH_MANAGED_WRITE=1` – permit writes (otherwise dry-run failure forcing legacy).
    * `PTVS_SAFE_ATTACH_MANAGED_FORCE=1` – force success (testing).
  * Fallback to legacy DLL injection on any failure site.

* **Telemetry**
  * Currently debug output only; structured event emission next (TM1).

* **Shared Code Convergence**
  * Parser + safe attach common result types shared.
  * Managed orchestrator mirrors native logic; future unification of thread state heuristics planned.

* **Legacy Compatibility**
  * Unchanged for ≤3.13 or when flags disabled.

---

## Managed-only Safe Attach Rollout Plan

### Phase 2A (Scaffolding – DONE)
* Gating + parser + result enums.

### Phase 2B (Core – DONE)
* Offsets address discovery + parsing.
* Basic thread state discovery.
* Memory write sequence (gated) + success path skip injection.

### Phase 2C (Robustness & Optimization – IN PROGRESS)
* Reattach cache (implemented) & validation (implemented basic).
* Partial-write fault injection (PENDING).
* Dynamic eval_breaker mask derivation (heuristic implemented; formal derivation pending RT1).
* Telemetry events (PENDING).
* Script path truncation telemetry (PENDING).
* Output window diagnostics (PENDING).

### Phase 2D (Enable by Default – PENDING)
* Success metrics & latency thresholds.

### Phase 2E (Minimal Loader – PENDING)
* Replace `ptvsd_loader.py` path with minimal bootstrap.

---

## Design Decisions & Risks

* **Safety**: Strict parsing + bounds checks before writes.
* **Fallback**: Immediate switch to legacy on any failure site.
* **Cache Validation**: Lightweight probe of remote support memory before reuse.
* **Stop-Bit Mask**: Heuristic; must confirm against runtime constants (RT1).
* **Telemetry Gap**: Must wire structured events before broad enablement.

---

## Telemetry & Logging (Planned vs Current)

| Aspect | Current | Planned |
|--------|---------|---------|
| Event Emission | Debug.WriteLine | Structured VS telemetry events |
| Failure Site Enum | Implemented | Same + counts, correlation id |
| Timing | Elapsed ms logged | Phase breakdown (resolve / parse / write) |
| Cache Reuse Flag | Logged | Event field |
| Truncation | Logged | Event field |
| Mask Selection | Logged | Event field + dynamic derivation (RT1) |

---

## Test Matrix Additions (Pending Implementation)

1. Safe attach success (3.14) end-to-end with writes.
2. Policy disabled (`RemoteDebugDisabled`).
3. Script truncation boundary case.
4. Partial write failure simulation (script path & eval breaker) – ensures fallback.
5. Reattach using cached thread state (with both valid and invalidated cases).
6. Future: stop-bit mask variation tests (after RT1).

---

## Next Actions (Revised)

1. **TM1**: Implement structured telemetry emission for managed safe attach attempts.
2. **PW1**: Partial write failure simulation hooks + tests.
3. **RT1**: Replace heuristic stop-bit mask with runtime-derived mask logic.
4. **CA1**: Strengthen cached thread state validation (struct field plausibility).
5. **MA-Tests**: Add managed safe attach unit & integration tests (success, fallback taxonomy, truncation, cache reuse).
6. **DX1**: Output window / Activity Log diagnostics summarizing failure site.
7. **ML1**: Minimal safe loader script & path size reduction.
8. **EN1**: Enable-by-default readiness review (success rate %, latency delta, failure taxonomy distribution).

---

## Legacy vs. 3.14 Safe Attach Comparison

| Aspect           | ≤3.13 Legacy            | 3.14 Safe Attach (PEP 768)     | Benefit               |
| ---------------- | ----------------------- | ------------------------------ | --------------------- |
| Discovery        | Heuristic symbol lookup | Structured `_Py_DebugOffsets`  | Lower fragility       |
| Entry            | Code injection calls    | Script path + flags + stop bit | Reduced crash surface |
| Thread State     | Current thread only     | Export + cache reuse           | Higher success rate   |
| Validation       | Minimal                 | Size, bounds, partial checks   | Safety                |
| Reattach         | Full re-run             | Cached `PyThreadState` reuse   | Performance           |
| Policy Awareness | Limited                 | `RemoteDebugDisabled` flag     | Governance            |
| Telemetry        | Sparse                  | (Planned) Structured events    | Diagnostics           |
| Compatibility    | Single path             | Dual path w/ fallback          | Smooth migration      |

---

## Done Definition (Phase 1)
* Native safe attach implemented & validated.

## Done Definition (Phase 2B – Managed Core)
* Offsets parse + write path + thread state discovery functional under flags.
* Legacy fallback maintained.

## Done Definition (Phase 2A – Managed Scaffolding)
* Parser / enums / gating.

---

(Visual roadmap diagram to be added after telemetry integration.)
