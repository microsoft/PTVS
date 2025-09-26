Here’s a combined, summarized version of **Python 3.14 Debugging Support in PTVS** with the roadmap and current status integrated for clarity:

---

# Python 3.14 Debugging Support in PTVS

## Overview

PTVS is adding full **Python 3.14 debugging support** across all modes:

* Managed-only (via debugpy)
* Mixed-mode / native Concord engine
* Remote & safe attach (PEP 768)
* Stepping, breakpoints, evaluation
* Version recognition and backward compatibility (3.5–3.13)

Key change: Leverage CPython 3.14's `debugger_support` structure for **safe attach** instead of legacy code injection.

---

## Goals

* Recognize Python 3.14 everywhere version gating exists.
* Implement safe remote attach using PEP 768 facilities.
* Update mixed-mode/native data model for 3.14 runtime changes.
* Preserve existing behavior for ≤3.13.
* Upgrade debugpy to 1.9.0 (done).
* Add comprehensive test coverage for launch, attach, stepping, evaluation.

Out of Scope: IntelliSense updates, profiling, or REPL changes beyond 3.14 version acceptance.

---

## High-Level Work Items

1. **Version Recognition & Constants** – Add `V314` enum, update predicates, paths, resources.
2. **debugpy Upgrade** – Bump to 1.9.0 (completed).
3. **_Py_DebugOffsets Extensions** – Parse new 3.14 fields: `eval_breaker`, `pending_call`, `script_path`, etc.
4. **Safe Attach Implementation** – Write script path + pending flag + stop bit via PEP 768; fallback to legacy if disabled/unavailable.
5. **Stepping Reliability** – Adjust for potential frame/code object changes.
6. **Resilience/Fallback** – Fail gracefully, revert to legacy if needed.
7. **Telemetry & Strings** – Safe attach success/failure codes, loader reuse flags.
8. **Docs & Samples** – Update mixed-mode/remote attach guides.
9. **Test Matrix** – Automated + manual validation across modes and versions.
10. **Final Validation** – Backward compatibility, performance baseline checks.

---

## Current State (as of 2025-09-24)

* **Safe Attach Protocol Implemented**

  * Memory writes for script path + pending flag + eval breaker stop bit.
  * Fallback chain for thread state discovery: main → enumerate → export → heuristic.
  * Loader reuse: existing `ptvsd_loader.py` invoked via safe attach for now; minimal loader planned later.
  * Telemetry logs selection method, attach result, truncation, reattach optimization.

* **Legacy Compatibility Confirmed**

  * Attach scripts for ≤3.13 remain functional.
  * Safe attach is version-gated and policy-aware (`RemoteDebugDisabled`).

* **Validation**

  * Positive/negative test cases for offsets, truncation, disabled flags, write failures.
  * Reattach optimization with cached `PyThreadState`.
  * Performance tests for attach latency pending.

---

## Design Decisions & Risks

* **Atomicity**: Minimal writes, no process suspension for now.
* **Safety**: Structured parsing prevents attaching to invalid memory regions.
* **Fallbacks**: Legacy attach path remains for older versions or disabled safe attach.
* **Risk Mitigation**: Defensive size/offset checks, telemetry for failure diagnostics, cached state reuse checks.

---

## Telemetry & Logging

* **AttachAttempt**: success/failure cause, selection path, truncation, loader reuse.
* **Offsets**: all pointer values and bounds.
* **LegacyCompat**: attach after safe attach regression checks.
* **Failure Site**: openProcess, writeScript, writeBreaker, etc.

---

## Test Matrix

* **Versions**: 3.9 → 3.14
* **Modes**: Managed-only, Mixed-mode, Remote attach, Safe attach (3.14)
* **Scenarios**: Stepping, breakpoints, async, exceptions, C extensions
* **OS**: Windows 10/11 x64, x86 optional

---

## Next Actions

1. **RT1**: Dynamic stop-bit mask confirmation or runtime derivation.
2. **CA1**: Cached `PyThreadState` revalidation before reuse.
3. **TM1**: Telemetry enrichment for failure site taxonomy + loader reuse flags.
4. **PW1**: Simulate partial write failures in tests.
5. **TH1**: Optional full interpreter/thread enumeration telemetry.

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

* Main-thread attach + fallback chain validated.
* Legacy attach regression tests passing.
* Full telemetry coverage + doc updates completed.
* Performance and correctness tests for 3.14 attach path passing.

---

Do you want me to create a **single visual roadmap diagram** for this so it’s easier to track phases and dependencies? It would make it simpler for your team to follow.
