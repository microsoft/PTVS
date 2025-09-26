# Mixed-Mode (Python + Native) Debugging Test Guide

## Overview
Mixed-mode debugging allows debugging Python code and native extension (C/C++) code in the same Visual Studio session.

## Prerequisites
- Visual Studio with C++ and Python workloads.
- CPython 3.x (x64) with matching architecture to VS.
- Native extension (.pyd) with PDB symbols (Debug build) if stepping into native desired.

## Enabling Mixed Mode
Set project property `EnableNativeCodeDebugging` to `True` (stored in the .pyproj) or check the checkbox on the Debug property page.

## Test Data Project
`TestData/MixedModeHelloWorld.sln` (added for automated tests) contains a simple Python script `Program.py` calling into a native extension (placeholder or pure Python fallback). Breakpoint is set at line 1 to validate Python engine load under native-enabled configuration.

## Manual Test Steps
1. Open solution `MixedModeHelloWorld.sln`.
2. Ensure interpreter selected (Python 3.x). 2.x not supported.
3. Enable native debugging in project properties.
4. Set breakpoint in `Program.py` line 1 and in native extension source (if available).
5. Press F5.
6. Verify Python breakpoint hits, then continue to native breakpoint (if extension built with symbols).
7. Inspect call stack for mixed frames.
8. Stop debugging; ensure clean shutdown.

## Automated Test (`DebugMixedModePythonProject`)
The UI test:
- Selects interpreter.
- Skips if Python 2.x.
- Opens `MixedModeHelloWorld.sln`.
- Inserts breakpoint line 1 of `Program.py`.
- Starts debugging and waits for break.
- Continues and asserts design mode on completion.

## Failure Diagnosis
| Symptom | Likely Cause | Action |
|---------|--------------|--------|
| Native breakpoint never binds | Symbols/PDB missing or wrong arch | Rebuild extension Debug/x64 |
| Launch fails complaining about version | Python 2.x used | Switch to Python 3.x |
| Immediate stop with no break | Startup file missing or not set | Set Startup File to `Program.py` |

## Notes
Current automated test only validates Python side break under native flag (extension optional). Future improvements can add a stub native project and ensure `EnableNativeCodeDebugging` property toggles engine list.
