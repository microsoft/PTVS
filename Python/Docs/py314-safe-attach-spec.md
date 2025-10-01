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

## Recent Managed Safe Attach Failure Analysis (Oct 2025)
**Symptoms:**
* Offsets parse stops after Strict/Extended/Relaxed and reports "no valid layout" even though flexible fallback could succeed.
* Attach UI test (`AttachBreakImmediately`) remains in run mode (no initial break).
* Mixed-mode project launch stays in design mode (`dbgDesignMode`) instead of breaking.
* Orchestrator reports offsets parse failure on 3.14 with header bytes starting `78 64 65 62 75 67 70 79` (cookie OK) and no 512 size tuple near early qwords.

**Root Causes:**
1. Flexible tuple reordering/variable size support added to parser but gated behind `PTVS_SAFE_ATTACH_FLEX_FALLBACK`; env not set in VS host → fallback never executed.
2. Orchestrator still hard-rejects non‑canonical `script_path_size != 512` (even when parser could flag NonCanonical).
3. Loader preference not updated: continues choosing `ptvsd_loader.py` instead of new `safe_attach_loader.py` for listen/connect scenarios → no debugpy session.
4. Option B (dynamic `debugpy.connect`) implemented, but adapter layer not yet passing `PTVS_DEBUGPY_CLIENT_PORT` → connect script path unused.
5. Mixed-mode (Concord) attempts managed safe attach prematurely; native + managed coordination incomplete → session never starts.

**Mitigations Applied / Pending:**
* Parser: flexible fallback implemented & gated (DONE). Need to default-enable or auto-enable upon strict failure (PENDING).
* Loader: `safe_attach_loader.py` added with verbose + initial breakpoint option (DONE). Need orchestrator search order update (PENDING).
* Break Immediately: loader now triggers `debugpy.breakpoint()` when `WAIT` or `PTVS_DEBUG_BREAK=1` (DONE). Adapter must set proper env (PENDING).
* Option B dynamic connect script generation in orchestrator (DONE). Adapter must allocate a port & set `PTVS_DEBUGPY_CLIENT_PORT` (PENDING).
* Mixed-mode guard: should disable managed safe attach until Concord parity (PENDING).
* Non‑canonical size acceptance: orchestrator must accept validated sizes (e.g. 0x300/0x1234) within sane cap (PENDING).

**Action Items:**
1. Auto-enable flex fallback: if strict + extended + relaxed fail AND cookie matches, internally enable flexible scan (ignore env). Add telemetry flag `flexUsed`.
2. Orchestrator size validation: replace hard check `size == 512` with `(size >= 512 && size <= 0x100000)` when `NonCanonicalSize` flagged.
3. Loader selection order: try `safe_attach_loader.py` (connect vs listen mode) → `ptvsd_loader.py` → temp bootstrap.
4. Adapter integration (Option B):
   * Allocate free port prior to orchestrator.
   * Set env: `PTVS_DEBUGPY_CLIENT_PORT`, `PTVS_DEBUGPY_CLIENT_HOST=127.0.0.1`, `PTVS_DEBUG_BREAK=1` (when AttachBreakImmediately), `PTVS_SAFE_ATTACH_FORCE_CONNECT=1`.
   * Switch `_debugInfo` to `DebugTcpAttachInfo` pointing to chosen port.
5. Mixed-mode detection: if native Python/Concord engine requested (or env `PTVS_MIXED_MODE=1`), set `PTVS_SAFE_ATTACH_MANAGED_DISABLE=1` and skip Option B.
6. Telemetry extension: record phases: parseMode (strict|extended|relaxed|flex), sizeCanonical (bool), loaderType (legacy|listen|connect), breakInjected, mixedModeBypass.
7. Add unit tests:
   * Parser: feed synthetic slab with non‑512 size; ensure flex accepted.
   * Orchestrator: simulate parsed NonCanonical → verify acceptance & loader write.
   * Adapter mock: env var propagation leads to dynamic connect script path chosen.
8. Regression test: ensure negative test `OffsetsParser_Fails_On_WrongSize` remains valid (flex gated or uses env disable).
9. Concord parity (phase 1): port flexible parser + NonCanonical acceptance (read-only) before enabling writes.

**Short-Term Fallback Guidance:**
* For failing environments set: `PTVS_SAFE_ATTACH_FLEX_FALLBACK=1`, `PTVS_SAFE_ATTACH_VERBOSE=1` to confirm flex acceptance.
* If attach must succeed immediately prior to adapter changes: disable managed safe attach (`PTVS_SAFE_ATTACH_MANAGED_DISABLE=1`) to revert to legacy injector.

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

## Loader Script Alignment (Managed Safe Attach)
**Problem:** Current managed safe attach writes the path of `ptvsd_loader.py` (if present) into the target process, or falls back to a temp script that only calls `debugpy.listen(('127.0.0.1',0))`. Neither reliably establishes a VS↔debugpy session:
* `ptvsd_loader.py` only imports symbols (`attach_process`, etc.) and asserts threading state; it does not start a listener nor connect.
* Temp fallback picks an ephemeral port (0) and never communicates the chosen port or waits for a client.
* Result: Safe attach may complete the memory write phase but no debugger connection is made (silent no‑op).

**Required Behavior:** Executed script must (a) start or connect a debug adapter endpoint (debugpy) and (b) coordinate port/session info with VS (deterministic or discoverable). No manual native callback available in pure safe attach path.

**Remediation Plan:**
1. Introduce `safe_attach_loader.py` (new file) with logic:
   * Read env vars: `PTVS_DEBUG_HOST` (default 127.0.0.1), `PTVS_DEBUG_PORT` (explicit port or 0), `PTVS_DEBUG_SESSION` (GUID), `PTVS_WAIT_FOR_CLIENT` (default 1).
   * If PORT unset or 0: choose free port via debugpy.listen; if chosen dynamically write the resolved port to a temp sidecar file path from `PTVS_DEBUG_PORT_FILE`.
   * Call `debugpy.listen((host, port))` then optional `debugpy.wait_for_client()` depending on env.
2. Orchestrator search order for loader path: `safe_attach_loader.py` → existing `ptvsd_loader.py` (legacy) → temp bootstrap.
3. Before writing script path: materialize `safe_attach_loader.py` into extension install dir if missing and set required env vars.
4. (Optional) For legacy ptvsd flows still needing `ptvsd_loader.py`, keep backward compatibility behind env var `PTVS_SAFE_ATTACH_LEGACY_PTvsd=1`.
5. Add post-write verification: if using new loader and dynamic port, poll sidecar file (or small pipe) with timeout; emit telemetry fields `loader=New/Legacy`, `dynPort=<n>`.
6. Update Concord path to mirror search + env semantics (shared constants & env names).
7. Tests:
   * Unit: verify buffer truncation logic with new script name.
   * Integration mock: confirm that dynamic port file gets written and parsed.
   * Negative: missing env vars → script still creates listener with defaults.

**Acceptance for Loader Fix:**
* Deterministic attach (fixed port) when `PTVS_DEBUG_PORT` set.
* Dynamic port discoverable within <500ms via sidecar when port=0.
* No regress in legacy injection path (explicit disable of safe attach still works).
* Telemetry includes loader type and success/failure site for script execution.

---

## Concord Debugger Integration Tasks (Planned)
Goal: Align native / Concord engine safe attach flow with managed implementation to ensure consistent behavior, diagnostics, and telemetry.

| Area | Task | Outcome |
|------|------|---------|
| Offsets Inference | Port `InterpreterWalkLocator` logic (C#) to Concord side or expose via shared assembly loaded by both | Single authoritative inference implementation |
| Candidate Validation | Replace native validation with managed `ThreadStateValidator` parity (field probes, pending flag, script buffer) | Reduced false positives |
| Cache Reuse | Implement cross-engine cache contract (shared key: pid + pyBase + version) with fast probe API | Faster reattach, consistent reuse semantics |
| Memory Abstraction | Introduce `IConcordProcessMemory` adapter mirroring `ProcessMemory` API (ReadPointer, TryReadU32) | Simplifies porting managed helpers |
| Write Plan | Factor write sequence into shared `RemoteWritePlan` (script path, pending flag, eval breaker) with verification stage | Uniform attach side effects and error codes |
| Logging | Adopt unified prefix `[PTVS][SafeAttach][Concord]` and single success/failure summary line | Easier log correlation across engines |
| Telemetry | Emit Attempt/Result events with phase flags (cacheUsed, inferred, walked, heuristicUsed, exportFallback) | Consistent metrics for reliability tracking |
| Heuristic Path | Gate native heuristic behind same env var `PTVS_SAFE_ATTACH_FORCE_HEURISTIC`; remove implicit fallback | Predictable resolution order |
| Export Fallback | Align bypass logic (3.14+ default) and env var overrides (`ALLOW_EXPORT`, `ALLOW_EXPORT_FALLBACK`) | Security surface parity |
| Fault Injection | Add Concord-side hooks to simulate partial write / read failures for resilience tests | Robustness validation |
| Negative Tests | Add controlled test host injecting malformed slabs, truncated script buffer, bogus pointer chains | Confidence against malformed memory |
| Version Gating | Ensure Concord path early-outs for <3.14 to legacy behavior with clear site code | Clear diagnostics |
| Result Object | Mirror `SafeAttachResult` shape (version, disabled, freeThreaded, reused, exportBypassed, site, message) | Simplified UI/report integration |
| Timing (Optional) | Lightweight phase stopwatch (disabled by default) behind `PTVS_SAFE_ATTACH_VERBOSE` | On-demand perf diagnostics |
| Shared Constants | Consolidate magic values (mask 0x20, size=512, cap offsets) into common constants module | Single source of truth |
| Unit Tests | Add Concord unit & integration tests exercising inference, cache miss/hit, failure sites | Test parity |

### Concord Migration Phases
1. Read-only validation: implement inference + validation without performing writes (dry-run mode, asserts parity with managed discovery).  
2. Full write enablement: integrate write plan & post-write verification.  
3. Telemetry + logging unification.  
4. Fault injection & negative scenarios.  
5. Cleanup: remove duplicated native heuristics superseded by deterministic walk.  

### Acceptance Criteria (Concord)
* ≥95% of managed safe attach success scenarios replicate on Concord in CI.
* Export fallback invoked <2% of 3.14+ attaches (only when inference + cache fail and env var allows).
* Zero false positives in negative test suite (malformed slabs / corrupted pointers).
* Unified structured success/failure log lines present exactly once per attempt.
* Telemetry events emitted for 100% of attempts with consistent schema.

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
