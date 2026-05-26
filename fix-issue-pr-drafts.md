# Fix-issue PR drafts

> One PR draft per `fix-watson-*` / `fix-issue-*` branch in this workspace. Each draft includes a PR title and a PR description with the issue link, context, the issue we hit, and the fix that landed.
>
> All issue numbers below link to the DevDiv ADO instance: `https://devdiv.visualstudio.com/DevDiv/_workitems/edit/<id>`.

---

## 1. `fix-watson-1222779-conda-history-watcher`

### PR title

Fix Watson #1222779: snapshot `_historyWatcherTimer` to avoid NRE on dispose race

### PR description

**Issue:** [#1222779](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1222779) — `System.NullReferenceException` in `CondaPackageManager._historyWatcher_Changed`.

**Context**

`CondaPackageManager` uses a `FileSystemWatcher` to detect when conda writes to its `history` file and a debouncing `Timer` (`_historyWatcherTimer`) to coalesce rapid edits before triggering a package re-enumeration. During VS shutdown or when the underlying environment is uninstalled, `Dispose()` sets `_historyWatcherTimer = null` while the watcher's callback or the timer's own Elapsed event is still in flight on another thread.

**Issue**

Both `_historyWatcher_Changed` and `_historyWatcherTimer_Elapsed` dereferenced `_historyWatcherTimer` directly. If `Dispose()` ran between the null-check and the dereference (or even after the callback was already inside the method), the field read returned `null` and the next member access threw `NullReferenceException`, which propagated out of the watcher callback and crashed VS.

**Fix**

Snapshot `_historyWatcherTimer` into a local at the top of both handlers, null-check the local, and operate on the local for the rest of the method. The dispose path can still null the field — the in-flight callback now operates against a stable reference and exits cleanly when the field has already been nulled.

Files changed:
- `Python/Product/VSInterpreters/PackageManager/CondaPackageManager.cs`

---

## 2. `fix-watson-1229982-pip-onrenamed-pathtoolong`

### PR title

Fix Watson #1229982: catch `PathTooLongException` in `PipPackageManager` event handlers

### PR description

**Issue:** [#1229982](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1229982) — `System.IO.PathTooLongException` in `PipPackageManager.OnRenamed`.

**Context**

`PipPackageManager` watches the interpreter's `site-packages` directory with a `FileSystemWatcher` to know when packages are installed, uninstalled, or renamed. The handlers read `RenamedEventArgs.FullPath` / `OldFullPath` (and `FileSystemEventArgs.FullPath` for the change path).

**Issue**

Those properties are lazy: under the hood they call `Path.NormalizePath`, which enforces the legacy `MAX_PATH` limit on .NET Framework. When the user has packages whose absolute path crosses 260 characters (common with deep monorepos, nested virtualenvs, or Windows long-path-disabled machines), the very first property read throws `PathTooLongException` — before the handler has a chance to do any defensive validation. The exception escapes through the watcher's event invocation list and crashes VS.

**Fix**

Wrap the path-property reads in `OnRenamed` and `OnChanged` in `try { … } catch (PathTooLongException) { return; }`. The package watcher is best-effort — silently dropping a single event for an unreachably-long path is the correct behaviour because we would not be able to act on the path anyway.

Files changed:
- `Python/Product/VSInterpreters/PackageManager/PipPackageManager.cs`

---

## 3. `fix-watson-1345469-processoutput-envvars`

### PR title

Fix Watson #1345469: use `ProcessStartInfo.Environment` to tolerate case-insensitive duplicate env vars

### PR description

**Issue:** [#1345469](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1345469) — `System.ArgumentException` ("Item has already been added. Key in dictionary…") thrown out of `ProcessOutput`.

**Context**

`ProcessOutput` (PTVS's wrapper around `Process.Start` used for invoking `python.exe`, `pip`, `conda`, etc.) copies the caller's requested environment variables into the child process's `ProcessStartInfo` so that operations like `pip install` see the right `PATH`, `PYTHONHOME`, etc.

**Issue**

The legacy `ProcessStartInfo.EnvironmentVariables` property is backed by a `StringDictionary`, which is **case-sensitive** on key comparison. Windows environment variables are case-insensitive, so a process that has both `Path` and `PATH` (perfectly legal on Windows) blew up the moment we tried to write the second one — `ArgumentException` "Item has already been added."

**Fix**

Switch the assignment from `psi.EnvironmentVariables[k] = v` to `psi.Environment[k] = v`. `ProcessStartInfo.Environment` (available since .NET 4.6) returns an `IDictionary<string, string>` with `OrdinalIgnoreCase` comparison, matching Windows semantics. A later assignment with a differently-cased key overwrites the earlier one cleanly.

The fix is applied consistently to all three copies of `ProcessOutput.cs` in the repository:

Files changed:
- `Common/Product/SharedProject/ProcessOutput.cs`
- `Python/Product/Common/Infrastructure/ProcessOutput.cs`
- `Python/Product/Cookiecutter/Shared/Infrastructure/ProcessOutput.cs`

---

## 4. `fix-watson-1446269-reanalyze-uithread`

### PR title

Fix Watson #1446269: marshal `ReanalyzeProject_Notify` to UI thread

### PR description

**Issue:** [#1446269](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1446269) — `System.Runtime.InteropServices.COMException` (`RPC_E_WRONG_THREAD`) thrown from `PythonProjectNode.OnReanalyzeProject_Notify`.

**Context**

`PythonProjectNode` raises a `ReanalyzeProject_Notify` event when interpreter/search-path changes invalidate the analysis cache. Subscribers include hierarchy / info-bar code that ultimately calls into VS shell COM objects (which are STA / UI-thread affine).

**Issue**

The event was being raised on whichever thread caused the change — frequently a background thread coming out of a `Timer`, a file-watcher, or an `async` continuation. When a subscriber transitively touched a UI-thread-only COM object, COM threw `RPC_E_WRONG_THREAD`, the exception escaped through `DoNotWait`, and VS crashed.

**Fix**

Marshal the raise of `ReanalyzeProject_Notify` to the UI thread:

```csharp
Site.GetUIThread().InvokeAsync(() => {
    if (IsClosed) { return; }
    ReanalyzeProject_Notify?.Invoke(this, EventArgs.Empty);
}).DoNotWait();
```

The `IsClosed` re-check inside the continuation handles the case where the project closes between scheduling and invocation. The outer call site additionally catches `ObjectDisposedException` from `Site` so that a teardown race doesn't replace one crash with another.

This change pairs with the cluster-D fix (`fix-watson-clusterD-task-extensions`), which silences benign disposal exceptions inside `DoNotWait` so that subscriber-side teardown races don't bubble back here.

Files changed:
- `Python/Product/PythonTools/PythonTools/Project/PythonProjectNode.cs`

---

## 5. `fix-watson-1459718-processservices-start`

### PR title

Fix Watson #1459718: tolerate `Win32Exception` when launching a missing interpreter

### PR description

**Issue:** [#1459718](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1459718) — `System.ComponentModel.Win32Exception` thrown out of `ProcessServices.Start`.

**Context**

`ProcessServices` is the small shim PTVS uses to launch helper processes (most commonly the user's Python interpreter for one-shot operations like `python -c "…"` to detect installed packages). Its `Start(ProcessStartInfo)` / `Start(string)` methods called `Process.Start` directly with no defensive handling.

**Issue**

If the configured interpreter has been uninstalled, moved, or never existed in the first place (broken `requirements.txt` workflows, stale config from a previous solution, antivirus quarantining the executable), `Process.Start` throws `Win32Exception` ("The system cannot find the file specified") synchronously. Because no caller wrapped the call in try/catch, the exception escaped through `Task.Run` continuations and crashed VS via `DoNotWait`.

**Fix**

Wrap `Process.Start` in `try { … } catch (Win32Exception) { return null; } catch (InvalidOperationException) { return null; }` in both overloads, and update `ExecuteAndCaptureOutputAsync` (the only in-tree caller that consumes the returned `IProcess`) to short-circuit on the null. All other call sites (only `PythonLibraryPath.GetSearchPathsAsync`) already go through `ExecuteAndCaptureOutputAsync` and inherit the new behaviour transparently.

Files changed:
- `Python/Product/Common/Core/OS/ProcessServices.cs`

---

## 6. `fix-watson-1488201-cookiecutter-waitforoutput`

### PR title

Fix Watson #1488201: surface Cookiecutter install failures without crashing VS

### PR description

**Issue:** [#1488201](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1488201) — `Microsoft.CookiecutterTools.Model.ProcessException` thrown out of `CookiecutterClient.CreateCookiecutterEnv`.

**Context**

The Cookiecutter tool window provisions a private virtualenv on first use so it can `pip install cookiecutter` without polluting the user's system Python. `CookiecutterClient.CreateCookiecutterEnv` spawns `python -m venv` and waits for it to finish. The caller, `CookiecutterViewModel.EnsureCookiecutterIsInstalledAsync`, has an outer `catch (Exception ex) when (!ex.IsCriticalException())` filter intended to translate any failure into a user-visible "install failed" status.

**Issue**

When `python -m venv` failed for benign reasons (offline, blocked by EDR, custom interpreter without `venv`, etc.), `CreateCookiecutterEnv` caught the `ProcessException` and **re-wrapped it as `CriticalException`**. The outer filter `when (!ex.IsCriticalException())` then refused to catch it, the exception escaped the message-loop boundary, and VS crashed.

**Fix**

Three coordinated changes:

1. Extract the wait-and-collect-output logic into a new helper `WaitForCookiecutterInstallOutput` that returns a structured `(exitCode, output)` tuple and throws a plain `InvalidOperationException` (with a localized message) on failure — *not* a `CriticalException`.
2. Remove the offending `catch (ProcessException) { throw new CriticalException(...); }` in `CreateCookiecutterEnv` and the two related call sites; let the new `InvalidOperationException` propagate.
3. Add a localized `Strings.CookiecutterInstallFailed` resource so the user sees a friendly status string instead of a stack trace.

After the change, install failures fall through the outer `catch (Exception ex) when (!ex.IsCriticalException())` filter as intended, set `InstallingStatus = Failed`, and surface a clean error in the tool window.

Files changed:
- `Python/Product/Cookiecutter/Model/CookiecutterClient.cs`
- `Python/Product/Cookiecutter/Strings.resx`
- `Python/Product/Cookiecutter/Strings.Designer.cs`

---

## 7. `fix-watson-1641198-hierarchy-closedoc`

### PR title

Fix Watson #1641198: do not throw when `CloseSolutionElement` fails during teardown

### PR description

**Issue:** [#1641198](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1641198) — `System.Runtime.InteropServices.COMException` (`E_FAIL`) thrown from `HierarchyNode.CloseDocument`.

**Context**

`HierarchyNode.CloseDocument` is called during project unload / solution close to release the document data backing a hierarchy node. It asks the running document table to close the element via `IVsSolution.CloseSolutionElement`.

**Issue**

`CloseSolutionElement` can legitimately fail with `E_FAIL` / `E_UNEXPECTED` during shutdown (the solution may already be partway through disposal, the document data may have been released by another path, etc.). The original code wrote:

```csharp
ErrorHandler.ThrowOnFailure(soln.CloseSolutionElement(...));
```

`ThrowOnFailure` converts any non-success HRESULT into a `COMException`, which then propagated through the teardown call stack and crashed VS. The failure was harmless — the element was already closing — but the exception was not.

**Fix**

Replace the unconditional `ThrowOnFailure` with a null-check on `soln`, capture the HRESULT into a local, and `Debug.Assert` only that the HRESULT is one of the expected values (`Succeeded`, `E_FAIL`, `E_UNEXPECTED`). The Debug assertion still surfaces *unexpected* failures (`E_INVALIDARG`, etc.) during local development without crashing customer VS. The `ppunkDocData` `IUnknown` release path is preserved unchanged.

Files changed:
- `Common/Product/SharedProject/HierarchyNode.cs`

---

## 8. `fix-watson-1812702-stream-intercepter`

### PR title

Fix Watson #1812702: catch broken-pipe `IOException` in `StreamIntercepter`

### PR description

**Issue:** [#1812702](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1812702) — `System.IO.IOException` (`ERROR_BROKEN_PIPE`) thrown out of `StreamIntercepter.Read`.

**Context**

`StreamIntercepter` is a `Stream` decorator PTVS wraps around the StreamJsonRpc transport going to Pylance. It exists so we can tee the raw bytes into a diagnostic log for support cases. The decorator forwards `Read` / `Write` / `Flush` to the underlying transport stream and invokes a logging callback with each chunk.

**Issue**

When Pylance exits (crash, normal shutdown, user closes solution, OS terminates the process), the named pipe / socket underlying the transport stream becomes invalid. The next `Read`/`Write`/`Flush` on the inner stream throws `IOException` (or `ObjectDisposedException` if VS has already disposed it on another thread). Because the decorator didn't catch anything, the exception propagated up through `StreamJsonRpc`'s read loop and ultimately through `DoNotWait`, crashing VS.

**Fix**

Wrap each forwarded operation in `try { … } catch (IOException) { … } catch (ObjectDisposedException) { … }`:

- `Read` returns `0` on failure. Zero is the standard "EOF" signal on a `Stream`, so `StreamJsonRpc` cleanly raises its `Disconnected` event through normal machinery rather than throwing.
- `Write` and `Flush` swallow the exception (the bytes are gone either way; logging is best-effort).

The logging callback is still invoked after a successful read so the tee log captures everything up to the disconnect.

Files changed:
- `Python/Product/PythonTools/PythonTools/LanguageServerClient/StreamHacking/StreamIntercepter.cs`

---

## 9. `fix-watson-2325087-invoke-connectionlost`

### PR title

Fix Watson #2325087: tolerate `ConnectionLostException` in `InvokeWithParametersAsync`

### PR description

**Issue:** [#2325087](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2325087) — `StreamJsonRpc.ConnectionLostException` thrown out of `PythonLanguageClient.InvokeWithParametersAsync`.

**Context**

`PythonLanguageClient` exposes thin helpers (`InvokeWithParametersAsync`, `NotifyWithParametersAsync`) that the rest of PTVS calls to issue LSP requests/notifications to Pylance. Both helpers forward through `_rpc` (the `StreamJsonRpc.JsonRpc` instance).

**Issue**

The Pylance process can exit at any time (crash, OOM, user terminates it). When that happens, `StreamJsonRpc` aborts every in-flight request with `ConnectionLostException`. PTVS issues these RPCs from many UI-driven paths (completion, hover, navigate-to) and from background services; none of them caught `ConnectionLostException`, so the first request after a Pylance crash propagated all the way out and took VS down. A concurrent disposal of `_rpc` would also cause an `ObjectDisposedException` with the same fatal outcome.

**Fix**

Update both `InvokeWithParametersAsync<R>` and `NotifyWithParametersAsync`:

1. Snapshot `_rpc` into a local before any awaits (eliminates a separate NRE race where another thread nulls the field).
2. Short-circuit with `return null` / `return` when the local is null.
3. Wrap the RPC call in `try { … } catch (ConnectionLostException) { … } catch (ObjectDisposedException) { … }` and return `null` / no-op on failure.

`InvokeWithParametersAsync<R>` is now constrained `where R : class` so the `null` return is type-safe. Callers (LSP feature implementations) already render "no result" gracefully when the underlying request returns no completions/hovers/etc., so returning `null` simply degrades the affected feature instead of crashing VS.

Files changed:
- `Python/Product/PythonTools/PythonTools/LanguageServerClient/PythonLanguageClient.cs`

---

## 10. `fix-watson-2455159-lsp-getsettings-dispose`

### PR title

Fix Watson #2455159: guard `PythonLanguageClient` timer and `GetSettings` against post-dispose NRE

### PR description

**Issue:** [#2455159](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2455159) — `System.NullReferenceException` thrown from `PythonLanguageClient.GetSettings`.

**Context**

`PythonLanguageClient` subscribes to `PythonAnalysisOptions.Changed` / `PythonAdvancedEditorOptions.Changed`. When the user edits options, a debouncing `Timer` fires after a short delay and calls `TriggerWorkspaceUpdateConfig`, which in turn calls `GetSettings()` to build a `workspace/didChangeConfiguration` payload. `GetSettings` reads `_analysisOptions` and `_advancedEditorOptions` and iterates `_clientContexts`.

**Issue**

During VS shutdown, `Dispose()` nulled out `_analysisOptions`, `_advancedEditorOptions`, and the client-context collection. If the debouncing timer was already mid-fire when dispose ran, the callback raced the field nulling and `GetSettings` threw `NullReferenceException` on its very first field dereference. The timer itself was also still running after `Dispose()` returned, so another tick could fire seconds later and hit the same race again.

**Fix**

Three coordinated guards:

1. Add a `private volatile bool _disposed` flag (volatile so cross-thread visibility is guaranteed).
2. In the `Dispose` action: set `_disposed = true` **first**, then stop the timer, then null the fields. The strict ordering means any in-flight callback that re-enters either method sees `_disposed == true` before touching the about-to-be-nulled fields.
3. Wrap the timer callback body in `try { … } catch { … }` and short-circuit `TriggerWorkspaceUpdateConfig` and `GetSettings` on `_disposed`. Both methods now return early/no-op when the client is already disposed.

Files changed:
- `Python/Product/PythonTools/PythonTools/LanguageServerClient/PythonLanguageClient.cs`

---

## 11. `fix-watson-clusterB-filewatcher-listener`

### PR title

Fix Watson cluster B (#1490074, #2014172, #2017229): race in `FileWatcher.Listener.OnFileChanged`

### PR description

**Issue:** Three Watsons, all with the same root cause:

- [#1490074](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1490074) — `System.ObjectDisposedException` from `Listener.OnFileChanged`
- [#2014172](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2014172) — `StreamJsonRpc.ConnectionLostException` from `Listener.OnFileChanged`
- [#2017229](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2017229) — `System.NullReferenceException` from `Listener.OnFileChanged`

**Context**

`FileWatcher.Listener` watches the user's workspace and forwards file change notifications to Pylance via `workspace/didChangeWatchedFiles` so that Pylance can re-analyze affected files. The listener holds a reference to the language client's `_rpc` and calls `NotifyWithParameterObjectAsync` for each batch of changes.

**Issue**

Three distinct failure modes, all on the same line of code:

1. If Pylance disposed between the file-watcher event and the call, dereferencing `_rpc` threw `ObjectDisposedException` (#1490074).
2. If the connection to Pylance dropped during the await, the call threw `ConnectionLostException` (#2014172).
3. If another thread nulled `_rpc` between the early-out null-check and the actual call, dereferencing the field threw `NullReferenceException` (#2017229).

All three escaped the file-watcher callback (a non-async-friendly event invocation) and crashed VS.

**Fix**

A single coordinated change inside `Listener.OnFileChanged` addresses all three failure modes:

1. Snapshot `_rpc` into a local `rpc` **before** the early null-check. Subsequent code uses only the local, so a concurrent null of `_rpc` cannot cause an NRE (#2017229 fixed).
2. Wrap the `await rpc.NotifyWithParameterObjectAsync(...)` in `try { … } catch (ObjectDisposedException) { … } catch (ConnectionLostException) { … }` (#1490074, #2014172 fixed).

Dropping a single file-change notification on disposal is benign: the next save event will re-trigger Pylance's incremental analysis. The previously-fatal disposal race is now a no-op.

Files changed:
- `Python/Product/PythonTools/PythonTools/LanguageServerClient/FileWatcher/Listener.cs`

---

## 12. `fix-watson-clusterD-task-extensions`

### PR title

Fix Watson cluster D (#1497512, #1487204, #1363565): silence LSP teardown exceptions in `DoNotWait`

### PR description

**Issue:** Three Watsons, all caused by `DoNotWait` re-raising exceptions that originate in disposed/disconnected LSP infrastructure:

- [#1497512](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1497512) — `System.ObjectDisposedException` in `Microsoft.PythonTools.Common.dll!Unknown`
- [#1487204](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1487204) — `StreamJsonRpc.ConnectionLostException` in `Microsoft.PythonTools.Common.dll!Unknown`
- [#1363565](https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1363565) — `System.ObjectDisposedException` in `Microsoft.PythonTools.Common.dll!Unknown`

Together these three Watsons account for **~8.2k hits**, by far the largest crash signal of the entire fix-watson series.

**Context**

`Task.DoNotWait()` is PTVS's "fire-and-forget" extension method. It attaches a continuation to the task that:

- If the task ran on a sync context, re-throws the fault on that context (`ReThrowTaskException`).
- Otherwise, re-throws the fault on the thread pool (`DoNotWaitThreadContinuation`).

Both paths assume any thrown exception represents a programmer mistake worth crashing on. This is the right policy for genuine bugs — but several PTVS code paths legitimately race against language-server teardown and surface benign disposal/disconnection exceptions that should be swallowed silently.

**Issue**

When the LSP transport went away (Pylance exited, solution closed, VS shutting down), in-flight `DoNotWait`-wrapped tasks completed faulted with `ObjectDisposedException` or `StreamJsonRpc.ConnectionLostException`. `DoNotWait` faithfully re-raised them to the runtime, which crashed VS. The root-cause sites were all over the codebase — every async LSP send-and-forget was a potential Watson — so fixing them one at a time would have been an open-ended whack-a-mole.

**Fix**

Add a single chokepoint: a static `_silencedExceptionTypes` table inside `TaskExtensions` listing exception types that represent benign LSP teardown. Today it contains `typeof(ObjectDisposedException)` and `typeof(StreamJsonRpc.ConnectionLostException)`.

A new helper `IsSilencedException(Exception ex)` walks the table and returns true if `t.IsAssignableFrom(ex.GetType())` for any entry — so derived exceptions are caught too. Both fault paths (`ReThrowTaskException` and `DoNotWaitThreadContinuation`) consult the helper before re-raising and skip silenced exceptions.

`OperationCanceledException` is intentionally **not** silenced — cancelled tasks have `IsFaulted == false` so they never reach the rethrow path. Critical/genuine bug exceptions like `InvalidOperationException` are also intentionally **not** silenced, so the existing `TaskExtensionsTests.DoNotWait` test (which asserts that `InvalidOperationException` is propagated) still passes.

This single-file change is the highest-ROI fix in the Watson series — it converts ~8.2k crashes into silent disposal no-ops at the framework level, and it complements every other fix-watson branch because it provides a backstop for any disposal race we missed in our per-site fixes.

Files changed:
- `Python/Product/Common/Infrastructure/TaskExtensions.cs`

Existing test that still passes:
- `Python/Tests/Core/TaskExtensionsTests.cs::DoNotWait`S

---

## Summary

| # | Branch | Issue(s) | One-line title |
|---|---|---|---|
| 1 | `fix-watson-1222779-conda-history-watcher` | #1222779 | Snapshot `_historyWatcherTimer` to avoid NRE on dispose race |
| 2 | `fix-watson-1229982-pip-onrenamed-pathtoolong` | #1229982 | Catch `PathTooLongException` in `PipPackageManager` event handlers |
| 3 | `fix-watson-1345469-processoutput-envvars` | #1345469 | Use `ProcessStartInfo.Environment` to tolerate case-insensitive duplicate env vars |
| 4 | `fix-watson-1446269-reanalyze-uithread` | #1446269 | Marshal `ReanalyzeProject_Notify` to UI thread |
| 5 | `fix-watson-1459718-processservices-start` | #1459718 | Tolerate `Win32Exception` when launching a missing interpreter |
| 6 | `fix-watson-1488201-cookiecutter-waitforoutput` | #1488201 | Surface Cookiecutter install failures without crashing VS |
| 7 | `fix-watson-1641198-hierarchy-closedoc` | #1641198 | Do not throw when `CloseSolutionElement` fails during teardown |
| 8 | `fix-watson-1812702-stream-intercepter` | #1812702 | Catch broken-pipe `IOException` in `StreamIntercepter` |
| 9 | `fix-watson-2325087-invoke-connectionlost` | #2325087 | Tolerate `ConnectionLostException` in `InvokeWithParametersAsync` |
| 10 | `fix-watson-2455159-lsp-getsettings-dispose` | #2455159 | Guard `PythonLanguageClient` timer and `GetSettings` against post-dispose NRE |
| 11 | `fix-watson-clusterB-filewatcher-listener` | #1490074, #2014172, #2017229 | Race in `FileWatcher.Listener.OnFileChanged` |
| 12 | `fix-watson-clusterD-task-extensions` | #1497512, #1487204, #1363565 | Silence LSP teardown exceptions in `DoNotWait` |
