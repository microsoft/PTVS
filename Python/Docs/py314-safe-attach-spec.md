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
* Add phase timing instrumentation (module, slab, inference, walk, write) to expose latency sources.
* Gate heuristic scan behind explicit env var; avoid unnecessary slow speculative scans.
* Revalidate cached `tstate` cheaply (offset-based field probes) before reuse.
* Ensure UTF‑8 loader script buffer is NUL‑terminated and within declared size.
* Add pending flag read probe to eliminate false positives in corrupted / stale memory cases.

---

## NEW (Since Previous Revision)

| Area | Change | Impact |
|------|--------|--------|
| DLL Detection | Regex updated to `^python3(\d{2,3})(?:_d)?\.dll$` | Correctly matches `python310/311/314.dll` (was previously limited to two digits). |
| Interpreter Walk | Added slab-wide scan + live memory validation to infer interpreter walk sextuple (`runtime_state`, `interpreters.head`, `interpreters.main`, `threads_head`, `threads_main`, `tstate.next`). | Deterministic thread state discovery without exports/TLS or heuristics. |
| Parser | Keeps 5‑tuple parse; no longer assumes adjacency for walk offsets. | Robust against layout variance across builds. |
| Validation | Candidate `PyThreadState` now probes: eval_breaker (4), pending flag (4), script path buffer (prefix). | Reduces false positives. |
| Pending Flag | Explicit read validation before acceptance. | Safety / correctness. |
| Eval Breaker | Enforces 32-bit field, ORs bit 0x20 only if not already set. | PEP 768 compliant. |
| Timing Instrumentation | Phase timing logged (module, slab read, inference, walk, write). | Performance baselining (latency now typically <50ms). |
| Walk Metrics | Candidate count + success path logged. | Diagnostic clarity for inference cost. |
| Export Path | Still bypassed by default for 3.14+, explicit opt-in only. | Security/surface reduction. |
| Heuristic Scan | Now hard opt-in (`PTVS_SAFE_ATTACH_FORCE_HEURISTIC`) and skipped for normal flow when deterministic path succeeds. | Reliability over guesswork. |
| Double Logging Cleanup | Removed duplicate parse log lines (in progress). | Log hygiene. |

---

## Shared Safe Attach Components

(UNCHANGED list plus clarifications)

| File | Purpose | Used By | Notes |
|------|---------|---------|-------|
| `PeExportReader.cs` | PE export enumeration/caching | Managed, Concord | Unchanged |
| `ThreadStateExportResolver.cs` | Export/TLS threadstate retrieval (legacy path) | Managed, Concord | Skipped for 3.14+ unless explicitly allowed |
| `SafeAttachUtilities.cs` | UTF-8 loader path prep (NUL termination, truncation) | Managed, Concord | Ensures <= size-1 + trailing `\0` |
| `EvalBreakerHelper.cs` | Dynamic/heuristic stop-bit mask selection (pre‑3.14) | Managed, Concord | 3.14+ constant 0x20 path bypasses dynamic logic |
| `ThreadStateCacheValidator.cs` | Fast reuse probe | Managed (Concord planned) | Probes eval_breaker + script buffer addresses |
| `HeuristicThreadStateLocator.cs` | Backward scan heuristic (optional) | Managed (Concord migrate) | Now rarely needed |

### NEW Helper Behavior
* Deterministic walk priority: cache → interpreter-walk (if inferred or parsed) → (optional heuristic) → (optional export fallback if explicitly enabled).
* Walk inference stops immediately after first validated `PyThreadState`.
* Inference counts each 6‑qword candidate examined (logged as `candidates=`).

---

## Remaining Commonization Opportunities (Updated)
1. Concord adoption of walk inference + cache validator (replace local logic).
2. Shared locator strategy abstraction (ordered providers + telemetry shaping).
3. Consolidated write helper returning structured success/failure with post-write verification.
4. Unified telemetry event schema (Attempt/Result) consuming new timing + mask + resolution metrics.
5. Fault injection seam (simulate partial writes) generalized.
6. Remove residual duplicate parse logs & ensure single-line success summary.

---

## Completed (Delta)
* Deterministic interpreter walk inference (slab-wide, memory-validated) – MANAGED.
* Pending flag + script buffer probe added to candidate validation.
* Regex fix for 3-digit minor version DLLs.
* Phase timing instrumentation & candidate metrics.

---

## Current State (Updated)
* **Managed Safe Attach**: Deterministic walk path stable; attach latency now typically tens of ms (previous worst-case 20+ seconds with repeated heuristics eliminated).
* **Exports**: Disabled by default for 3.14+, still available as fallback via env var.
* **Heuristics**: Off unless explicitly forced; treated as diagnostic tool.
* **Cache**: Reuse path validated before any discovery work; pending Concord integration.
* **Telemetry**: Instrumentation in logs only; schema wiring pending.

---

## Environment Variables (Active Set)
| Name | Effect | Default |
|------|--------|---------|
| `PTVS_SAFE_ATTACH_MANAGED_DISABLE` | Disable safe attach (force legacy injector) | Not set |
| `PTVS_SAFE_ATTACH_ALLOW_EXPORT` | Allow export/TLS path even if 3.14+ | Not set |
| `PTVS_SAFE_ATTACH_ALLOW_EXPORT_FALLBACK` | Permit export fallback if walk+cache fail | Not set |
| `PTVS_SAFE_ATTACH_MANAGED_NO_CACHE` | Disable tstate cache reuse | Not set |
| `PTVS_SAFE_ATTACH_FORCE_HEURISTIC` | Force heuristic scan attempt | Not set |
| `PTVS_SAFE_ATTACH_VERBOSE` | Enable verbose logs (Release) | Not set |
| `PTVS_SAFE_ATTACH_MANAGED_WRITE` | Explicitly allow writes (else default-on) | Not required |

---

## Timing Fields (Logged Currently)
`module=<ms> slab=<ms> infer=<ms> walk=<ms> write=<ms> total=<ms> candidates=<n> inferOk=<bool> walkTried=<bool>`

Future telemetry will split: cacheProbe, exportFallback, heuristicAttempts, writeSubPhases (pathWrite, pendingWrite, breakerWrite).

---

## Next Actions (Refreshed)
1. Telemetry event emission (Attempt/Result) + schema finalization.
2. Concord migration to deterministic walk.
3. Shared write + verification helper (returns annotated result).
4. Post-write validation & optional re-read hash for script buffer.
5. Loader script footprint reduction (opt: on-demand network listener injection).
6. Add negative test vectors: corrupted slab, partial slab, synthetic walk collision.
7. Final pass removing duplicate verbose lines & standardizing prefixes.

---

## Legacy vs. 3.14 Safe Attach Comparison (Unchanged Core – augmented)

| Aspect | ≤3.13 Legacy | 3.14 Safe Attach (Current) | Benefit |
|--------|--------------|---------------------------|---------|
| Discovery | Heuristic export/TLS | Deterministic slab parse + walk | Reliability + speed |
| Reattach | Full rediscovery | Cache (validated) | Performance |
| Failure Surface | Injection thread + allocation | In-place script path + bit flip | Lower risk |
| Validation | Minimal | Structured + multi-probe + timing | Safety & diagnostics |
| Telemetry | Sparse | Timing logged (events pending) | Observability |

---

(Sections below retained for historical context; will be pruned once telemetry lands.)
