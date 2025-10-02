# Safe Attach: Debugpy Requirements (Old vs New)

## Summary
The safe attach path no longer depends on the target Python environment having `debugpy` installed. The loader always prefers the VSIX-bundled copy and guarantees import by adjusting `sys.path` before attempting the import. This replaces older behavior that sometimes relied on the environment or multiple fallback loaders.

## Previous (Legacy) Attach Pipeline
- Loader script: `ptvsd_loader.py` (later transitional support for `debugpy`).
- Search order:
  1. Environment `sys.path` (user / venv installed `debugpy` could shadow bundled copy).
  2. VSIX extension install directory (added to `sys.path` by loader).
  3. Fallback: import `ptvsd` and alias as `debugpy` if `debugpy` import failed.
- Multiple potential loader files (`ptvsd_loader.py`, dynamic scripts, bootstrap stubs) increased ambiguity.
- Break / wait flags were varied (older env variable names) and sometimes duplicated logic.
- Failure modes: Missing loader file or blocked VSIX deployment produced timeouts (no early telemetry for missing loader).

## Current Safe Attach Pipeline
- Single canonical loader: `safe_attach_loader.py`.
- Loader path written into `_Py_DebugOffsets` script buffer by orchestrator.
- Deterministic import path adjustments:
  - Inserts its own directory at `sys.path[0]`.
  - If a `debugpy` package directory sits adjacent to the loader (VSIX-bundled), that path is explicitly inserted (ensures vendored copy loads even if environment has none or an older version later on `sys.path`).
- Import sequence:
  1. `import debugpy` (expected to succeed with bundled copy).
  2. Fallback: `import ptvsd as debugpy` (legacy) only if first import fails.
- No requirement to pre-install `debugpy` in user/venv.
- Environment variables (simplified):
  - `PTVS_DEBUG_HOST` (default `127.0.0.1`).
  - `PTVS_DEBUG_PORT` (`0` = dynamic allocation).
  - `PTVS_WAIT_FOR_CLIENT` (`1` causes wait + implicit initial breakpoint unless disabled).
  - `PTVS_DEBUG_BREAK` (forces initial breakpoint; effective OR with wait).
  - `PTVS_DEBUG_PORT_FILE` (writes chosen port for host discovery if needed).
  - `PTVS_SAFE_ATTACH_LOADER_VERBOSE` (verbose logging; default on, set to `0` to disable).
  - `PTVS_DEBUG_LOG_FILE` (optional log sink path).
- The orchestrator now fails early with telemetry if the canonical loader is missing (tests may still override via `PTVS_SAFE_ATTACH_LOADER_OVERRIDE`).
- Inline or legacy fallback generation removed by default (can be reintroduced only in test scenarios).

## Behavioral Differences
| Aspect | Old | New |
|--------|-----|-----|
| Loader files | Multiple (`ptvsd_loader.py`, dynamic scripts, bootstrap) | Single (`safe_attach_loader.py`) |
| Need environment `debugpy` | Optional; environment could shadow | Not required; VSIX copy always injected |
| Fallback to `ptvsd` | Common when `debugpy` missing | Still present but rarely triggered |
| sys.path manipulation | Loader inserted its directory; dynamic scripts inconsistent | Always inserts loader dir + adjacent `debugpy` explicitly |
| Break Immediately | Varied flags; sometimes separate connect/wait roles | Unified via `PTVS_WAIT_FOR_CLIENT` + `PTVS_DEBUG_BREAK` (either triggers breakpoint) |
| Telemetry on missing loader | Often silent ? timeout | Immediate failure with `ScriptBufferWrite` site + message |
| Dynamic connect vs listen roles | Both sides could attempt connect (race/timeouts) | Target always listens; host always connects |

## Migration Notes
- Remove any external install steps instructing users to `pip install debugpy` solely for attach.
- Update troubleshooting docs: If attach times out now, primary causes are listener creation failure (port bind) or blocked VSIX deployment—no longer missing Python package.
- For test harnesses that need to force a specific loader path, use `PTVS_SAFE_ATTACH_LOADER_OVERRIDE`.

## Troubleshooting Quick Reference
| Symptom | Old Likely Cause | New Likely Cause | Action |
|---------|------------------|------------------|--------|
| Timeout waiting for debugger | Wrong connect/listen role, missing loader, missing debugpy | Port blocked or listener creation failed | Examine safe attach log for `listen failed` / port reuse; retry with different port |
| Immediate `loader file not found` failure | Rarely surfaced | VSIX not deployed / file missing | Rebuild & deploy VSIX; verify file presence in extension folder |
| No breakpoint on attach | Flags mismatch or dynamic script skipped breakpoint | `PTVS_WAIT_FOR_CLIENT=0` and `PTVS_DEBUG_BREAK` unset | Set `PTVS_WAIT_FOR_CLIENT=1` or `PTVS_DEBUG_BREAK=1` |
| Falls back to ptvsd | Missing modern debugpy | Corrupted or absent bundled debugpy | Redeploy extension; check extension folder integrity |

## Recommended Documentation Updates
1. Replace legacy instructions referencing `ptvsd_loader.py` with `safe_attach_loader.py`.
2. Clarify no environment installation of debugpy is required.
3. Provide the simplified env var table above.
4. Remove steps about reverse connect scripts unless future reverse mode is implemented.
5. Add early failure telemetry codes mapping (e.g. `ScriptBufferWrite: loader file not found`).

---
Last updated: (auto-generated) Safe Attach debugpy requirements consolidation.
