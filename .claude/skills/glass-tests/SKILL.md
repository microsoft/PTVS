---
name: glass-tests
description: "Use when working with PTVS Glass tests: setup_glass.py, run_glass.py, Glass.TestAdapter, Newtonsoft.Json assembly mismatches, Azure Pipelines Glass cache, TestScript.xml, glass2.exe, and mixed-mode Python debugger tests."
argument-hint: "[setup|run|ci-failure|debug]"
---

# PTVS Glass Tests

## When to Use

Use this skill when setting up, running, debugging, or investigating CI failures for PTVS Glass tests. Glass tests exercise Visual Studio debugger behavior, including mixed-mode Python/native scenarios, and require Microsoft-internal drops for the Glass runner.

Link to existing human-facing documentation instead of duplicating it. Start with [Python/Tests/GlassTests/readme.txt](../../../Python/Tests/GlassTests/readme.txt), which links the internal Concord Glass docs and presentation.

## Key Files

- [Build/setup_glass.py](../../../Build/setup_glass.py): downloads `drop.exe`, Glass, NuGet tools, and vstest drops; extracts VSIX payloads; copies PTVS debugger bits; discovers installed Python versions; generates `.GlassTestProps` files; verifies the test listing.
- [Build/run_glass.py](../../../Build/run_glass.py): calls `copy_ptvs_output()`, verifies the runtime test root, and invokes `vstest.console.exe` with `/Parallel` and TRX logging.
- [Build/templates/run_tests.yml](../../../Build/templates/run_tests.yml): installs Python versions, restores or populates the `GlassTests` cache, acquires the Azure DevOps token, runs setup, runs tests, and publishes TRX results.
- [Python/Tests/GlassTests/PythonTests/PythonConcord.GlassTestRoot](../../../Python/Tests/GlassTests/PythonTests/PythonConcord.GlassTestRoot): root Glass test configuration.
- `Python/Tests/GlassTests/PythonTests/**/TestScript.xml`: individual debugger test scripts with commands, expected events, breakpoints, and source expectations.
- `GlassTests/`: generated/runtime directory for downloaded Glass bits and copied tests; do not treat it as the source of truth for test definitions.

## Local Workflow

1. Build PTVS first so the chosen build output contains `EnvironmentDiscover.exe`, `Microsoft.Python*`, and `DkmDebu*` files.
2. Prefer passing the build output explicitly, especially for VS 18 builds:

   ```powershell
   python Build\setup_glass.py --buildOutput BuildOutput\Debug18.0\raw\binaries
   ```

3. If setup needs internal drop access, provide an auth token environment variable or allow the interactive credential flow:

   ```powershell
   python Build\setup_glass.py --authTokenVariable SYSTEM_ACCESSTOKEN --buildOutput <path-to-raw-binaries>
   ```

4. List discovered tests after setup:

   ```powershell
   python Build\setup_glass.py --verifyListing
   ```

5. Run all tests or a single test:

   ```powershell
   python Build\run_glass.py --buildOutput BuildOutput\Debug18.0\raw\binaries
   python Build\run_glass.py --buildOutput BuildOutput\Debug18.0\raw\binaries Repr_Bytes-311-64
   ```

## CI Failure Workflow

1. Start with the PR log and `TestResults/PythonTests.trx`.
2. Classify the failure:
   - Setup/drop/auth failure before `vstest.console.exe` starts.
   - Discovery/adapter failure after `vstest.console.exe` starts but before individual tests run.
   - `glass2.exe` or `TestScript.xml` failure inside a specific test case.
3. Check [Build/templates/run_tests.yml](../../../Build/templates/run_tests.yml) to see whether `GlassTests` came from cache. Cache hits skip `setup_glass.py`, but `run_glass.py` still calls `copy_ptvs_output()`.
4. For assembly binding errors, inspect every `Newtonsoft.Json.dll` under `GlassTests` and compare assembly versions. `Glass.TestAdapter.dll` expects `Newtonsoft.Json, Version=13.0.0.0`; NuGet/vstest VSIX extraction can place older 9.x or 10.x copies in the runner tree. The compatible copy usually comes from `GlassTests\Glass\Newtonsoft.Json.dll`.
5. Do not assume a PTVS NuGet restore or package update will fix Glass runner dependency conflicts. The failing assembly may come from downloaded Glass/NuGet/vstest VSIX payloads, not from `packages/`.

## Debugging TestScript Failures

1. Reproduce one test at a time with `python Build\run_glass.py --buildOutput <path> <test-name>`.
2. Inspect the failing test's `TestScript.xml` and source files under `Python/Tests/GlassTests/PythonTests/`.
3. Compare the error log against expected events, source file names, line numbers, breakpoints, and function names in the script.
4. Look for generated logs, dumps, and copied debuggee files under the runtime `GlassTests\PythonTests\...\obj\<configuration>\` and `bin\<configuration>\` paths.
5. To debug Glass itself, set `GLASS_DEBUG=1`, rerun one test, attach Visual Studio to `glass2.exe`, and check assertions or event handling.
6. To debug with the real product, follow [Python/Tests/GlassTests/readme.txt](../../../Python/Tests/GlassTests/readme.txt): run PTVS under the debugger, open the generated test folder, mirror the `TestScript.xml` breakpoints, and use `launch.vs.json` with the intended interpreter.

## Common Pitfalls

- Full setup is Microsoft-internal because `drop.exe` downloads from internal DevDiv artifact drops.
- `run_glass.py` without `--buildOutput` historically probes 17.0 build output paths; pass `--buildOutput` for local VS 18 builds.
- `EnvironmentDiscover.exe not found` usually means the build output path is wrong or stale.
- `PythonConcord.GlassTestRoot` or `PythonEngine.regdef` missing means the runtime test copy is incomplete or setup did not run successfully.
- Cache hits can preserve stale `GlassTests` contents. Fixes that must run before test execution should be in the `run_glass.py` path, usually via `copy_ptvs_output()`.
- Runtime files under `GlassTests/` are generated and cacheable; source test changes belong under `Python/Tests/GlassTests/`.
