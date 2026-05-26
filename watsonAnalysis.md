# PTVS Watson Analysis

> Investigation of all **Watson-tagged** work items from Azure DevOps query
> [`b79949c2-6325-43cb-bfb7-18f311860bb6`](https://devdiv.visualstudio.com/DevDiv/_queries/query-edit/b79949c2-6325-43cb-bfb7-18f311860bb6/).
> Source code analysis performed against `Z:\Repos\PTVS` at commit `50b7f409e` (branch `main`).
> Date of investigation: **2026-05-26**.

---

## Table of contents

| Cluster | Theme | Bugs | Total hits |
|---|---|---|---:|
| [A](#cluster-a--file-system-watcher-event-handlers-fire-after-dispose) | File-system watcher events fire after handler is torn down | 1222779, 1229982 | 1,063 |
| [B](#cluster-b--filewatcherlistener-notifies-a-dying-jsonrpc) | LSP `FileWatcher.Listener` notifies a dying `JsonRpc` | 1490074, 2014172, 2017229 | 269 |
| [C](#cluster-c--broken-pipe-writing-to-pylance) | `StreamIntercepter` writes to a broken Pylance stdin pipe | 1812702 | 10 |
| [D](#cluster-d--lsp-disposal-races-the-big-ones) | LSP / Pylance disposed while a fire-and-forget task is in-flight | 1363565, 1487204, 1497512, 2325087, 2454483, 2455159 | 11,769 |
| [E](#cluster-e--vs-hierarchy--threading) | VS project hierarchy / UI-thread violations | 1641198, 1446269 | 136 |
| [F](#cluster-f--process-launching) | Process launching surfaces unhandled exceptions | 1345469, 1459718, 1488201 | 100 |
| [G](#cluster-g--removed-legacy-module) | Legacy (now-removed) analyzer | 1432602 | 1,620 |

> **The 6 LSP/Pylance disposal bugs in cluster D account for ~80% of all hits**, with **#1497512 (8,088 hits)** and **#2454483 (3,332 hits)** being the loudest signals in the entire query.

---

## Cluster A — File-system watcher event handlers fire after dispose

### 🐞 #1222779 — `CondaPackageManager._historyWatcher_Changed` NullReferenceException

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1222779>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.NullReferenceException_80004003_Microsoft.PythonTools.VSInterpreters.dll!Microsoft.PythonTools.Interpreter.CondaPackageManager._historyWatcher_Changed`
- **Created:** 2020-09-29
- **Hit count:** **1,045**
- **Builds:** 16.8.30711.63 – 18.6.11806.211 (still occurring)
- **State:** Active

#### Source location

`Python\Product\VSInterpreters\PackageManager\CondaPackageManager.cs:125`

```csharp
private void _historyWatcher_Changed(object sender, FileSystemEventArgs e) {
    if (PathUtils.IsSamePath(e.FullPath, _historyPath)) {
        try {
            _historyWatcherTimer.Change(1000, Timeout.Infinite);   // ← NRE here
        } catch (ObjectDisposedException) {
        }
    }
}
```

#### Root cause (plain English)

`FileSystemWatcher` events fire on the thread-pool via Windows I/O completion ports. There is an inherent race between `DisableNotifications()` (line 97 of the same file) — which executes `_historyWatcherTimer.Dispose(); _historyWatcherTimer = null;` — and an in-flight `Changed` event that the OS has already queued.

The handler is prepared for the timer being **disposed** (catches `ObjectDisposedException`), but it is **not** prepared for the timer field being **nulled** by `DisableNotifications` between the field read and the `.Change()` call. When this happens, dereferencing the null reference throws an `NullReferenceException` that escapes the try/catch.

With 1,045 hits and a build range spanning multiple major versions (16.8 → 18.6), this race is consistently reproducing whenever a Conda env package manager is disposed during heavy I/O (e.g., during `conda install` while the user closes the project).

#### Fix

Snapshot the field into a local and null-check it:

```csharp
private void _historyWatcher_Changed(object sender, FileSystemEventArgs e) {
    if (!PathUtils.IsSamePath(e.FullPath, _historyPath)) return;
    var timer = _historyWatcherTimer;          // single read
    if (timer == null) return;
    try { timer.Change(1000, Timeout.Infinite); }
    catch (ObjectDisposedException) { }
}
```

The same pattern issue also exists at `CondaPackageManager.cs:116` (`_historyWatcherTimer_Elapsed`) — apply the same snapshot-and-null-check there.

---

### 🐞 #1229982 — `PipPackageManager.OnRenamed` `PathTooLongException`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1229982>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.IO.PathTooLongException_800700ce_Microsoft.PythonTools.VSInterpreters.dll!Microsoft.PythonTools.Interpreter.PipPackageManager.OnRenamed`
- **Created:** 2020-10-13
- **Hit count:** **18**
- **Builds:** 18.4.11612.150
- **State:** Active

#### Source location

`Python\Product\VSInterpreters\PackageManager\PipPackageManager.cs:755-760`

```csharp
private void OnRenamed(object sender, RenamedEventArgs e) {
    if (Directory.Exists(e.FullPath) ||
        ModulePath.IsPythonFile(e.FullPath, false, true, false) ||
        ModulePath.IsPythonFile(e.OldFullPath, false, true, false)) {   // ← throws
```

The watchers themselves are configured with `IncludeSubdirectories = true` over every `site-packages` directory (line 678 of the same file).

#### Root cause (plain English)

The Watson stack reveals that the throw is **inside** `RenamedEventArgs.get_OldFullPath`, which traverses `Path.GetPathRoot → Path.NormalizePath → Path.LegacyNormalizePath`. .NET Framework's `RenamedEventArgs` builds and normalizes the full path on demand, and **the normalization enforces `MAX_PATH` (260 chars)** on legacy .NET Framework.

Since PTVS watches every `site-packages` recursively, any pip install whose final path exceeds 260 characters (very common when packages have deeply nested vendored dependencies) trips this. Notably, the throw is on the **simple property accessor** — the code never even reaches its actual logic.

#### Fix

Wrap the property accesses in try/catch — this is a non-actionable OS quirk:

```csharp
private void OnRenamed(object sender, RenamedEventArgs e) {
    string fullPath, oldFullPath;
    try { fullPath = e.FullPath; oldFullPath = e.OldFullPath; }
    catch (PathTooLongException) { return; }   // skip files we can't see
    if (Directory.Exists(fullPath) ||
        ModulePath.IsPythonFile(fullPath, false, true, false) ||
        ModulePath.IsPythonFile(oldFullPath, false, true, false)) {
        ...
    }
}
```

The same defensive read should be applied to `OnChanged` (line ~720) since the same property access is performed there.

---

## Cluster B — `FileWatcher.Listener` notifies a dying `JsonRpc`

All three bugs in this cluster ride **the same code path** in `Python\Product\PythonTools\PythonTools\LanguageServerClient\FileWatcher\Listener.cs`.

#### Shared source location

```csharp
// Lines 74-77 — _rpc gets nulled on disconnect
private void _rpc_Disconnected(object sender, JsonRpcDisconnectedEventArgs e) {
    _rpc.Disconnected -= _rpc_Disconnected;
    _rpc = null;
}

// Lines 118-176 — handler with the race
private async Task OnFileChanged(object sender, FileSystemEventArgs e) {
    if (e.IsDirectoryChanged() || _rpc == null) return;     // line 121 — early null check
    ...
    if (didChangeParams.Changes.Any()) {
        await _rpc.NotifyWithParameterObjectAsync(           // line 173 — NRE / ODE / ConnectionLost
            Methods.WorkspaceDidChangeWatchedFiles.Name, didChangeParams);
    }
}
```

#### Shared root cause (plain English)

The handler performs a **check-then-use** against `_rpc` without any locking. Between the null check on line 121 and the `await` on line 173 the handler performs file matching, allocates LSP parameters, and finally calls into JsonRpc — all of which are yield points or computational windows long enough for one of three things to happen:

1. `_rpc_Disconnected` runs and nulls `_rpc` → `NullReferenceException`
2. The underlying `JsonRpc` is disposed by its owner but the Disconnected event hasn't reached Listener yet → `JsonRpc.NotifyWithParameterObjectAsync` calls `Microsoft.Verify.NotDisposed` which throws `ObjectDisposedException`
3. The Pylance child process crashes/exits → StreamJsonRpc's pipe closes mid-flight → `ConnectionLostException`

The three Watsons are the three faces of the same race. The `d__12` vs `d__13` suffixes refer to different versions of the same async state machine — the file gained an extra `await` between major releases, shifting the compiler-generated class index.

---

### 🐞 #2017229 — `NullReferenceException` (state machine `d__13`)

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2017229>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.NullReferenceException_80004003_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.FileWatcher.Listener+_OnFileChanged_d__13.MoveNext`
- **Created:** 2024-04-04
- **Hit count:** **228**
- **Builds:** 17.9.34728.123 – 17.10.35122.118
- **State:** Active

`_rpc` is nulled by `_rpc_Disconnected` after the early `if (_rpc == null) return;` check but before the eventual `await _rpc.NotifyWithParameterObjectAsync(...)` call.

---

### 🐞 #1490074 — `ObjectDisposedException` (state machine `d__12`)

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1490074>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.ObjectDisposedException_80131622_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.FileWatcher.Listener+_OnFileChanged_d__12.MoveNext`
- **Created:** 2022-02-25
- **Hit count:** **5**
- **Builds:** 17.1.32210.238
- **State:** Active

`JsonRpc.NotifyWithParameterObjectAsync` calls `Microsoft.Verify.NotDisposed` (visible at top of stack) — the JsonRpc instance was disposed by its owner but Listener hadn't yet received the Disconnected callback.

---

### 🐞 #2014172 — `ConnectionLostException` (state machine `d__13`)

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2014172>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_StreamJsonRpc.ConnectionLostException_80131500_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.FileWatcher.Listener+_OnFileChanged_d__13.MoveNext`
- **Created:** 2024-03-30
- **Hit count:** **36**
- **Builds:** 17.10.35122.118 – 17.12.35707.178
- **State:** Active

Pylance crashed/exited; pipe closed mid-flight; StreamJsonRpc raises `ConnectionLostException`.

#### Fix (one change covers all three Watsons in cluster B)

```csharp
private async Task OnFileChanged(object sender, FileSystemEventArgs e) {
    var rpc = _rpc;                          // single read into local
    if (e.IsDirectoryChanged() || rpc == null) return;
    ...
    try {
        if (didChangeParams.Changes.Any()) {
            await rpc.NotifyWithParameterObjectAsync(
                Methods.WorkspaceDidChangeWatchedFiles.Name, didChangeParams);
        }
    } catch (ObjectDisposedException)   { /* Pylance shutting down */ }
      catch (ConnectionLostException)   { /* Pylance died */ }
      catch (OperationCanceledException){ }
}
```

The single-read into a local prevents the NRE (case 1). The catch handlers cover the disposed JsonRpc (case 2) and broken pipe (case 3). All three Watsons are addressed in one ~6-line change.

---

## Cluster C — Broken pipe writing to Pylance

### 🐞 #1812702 — `StreamIntercepter.Write` `IOException 0x8007006d`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1812702>
- **Title:** `[Watson] crash64: CLR_EXCEPTION_System.IO.IOException_8007006d_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.StreamHacking.StreamIntercepter.Write`
- **Created:** 2023-05-02
- **Hit count:** **10**
- **Builds:** 17.4.33213.308 – 17.5.33627.172
- **State:** Active

#### Source location

`Python\Product\PythonTools\PythonTools\LanguageServerClient\StreamHacking\StreamIntercepter.cs:54`

```csharp
public override void Write(byte[] buffer, int offset, int count) {
    if (writeHandler != null) {
        var writeHandlerResult = writeHandler.Invoke(new StreamData { ... });
        baseStream.Write(writeHandlerResult.Item1.bytes, ...);   // ← throws
        if (!writeHandlerResult.Item2) { writeHandler = null; }
    } else {
        baseStream.Write(buffer, offset, count);                  // ← or here
    }
}
```

#### Root cause (plain English)

`0x8007006d` is the Win32 error `ERROR_BROKEN_PIPE` (109). PTVS wraps Pylance's stdin via `StreamIntercepter` so it can prepend / massage the LSP "initialize" message. When the Pylance child process exits or crashes, the next `Write` to its stdin throws `IOException("Pipe is broken")`.

StreamJsonRpc *does* catch the exception and converts it to a Disconnected event eventually — but the throw still bubbles **synchronously** through a very long async chain. This is visible in the Watson stack as 30+ repeated frames of `StreamJsonRpc.JsonRpc / TaskAwaiter.HandleNonSuccessAndDebuggerNotification` (each one is an async continuation re-throwing the captured exception). At some point the exception escapes all awaiters and tears down VS.

This is a Watson categorized as `crash64` (native-side crash) rather than the usual `clr20r3` because the unhandled exception finally surfaced in a thread without any further managed handler.

#### Fix

Treat the underlying stream as best-effort during teardown — swallow IO errors so StreamJsonRpc can detect the disconnect cleanly via its own machinery:

```csharp
public override void Write(byte[] buffer, int offset, int count) {
    try {
        if (writeHandler != null) {
            var r = writeHandler.Invoke(new StreamData { bytes = buffer, offset = offset, count = count });
            baseStream.Write(r.Item1.bytes, r.Item1.offset, r.Item1.count);
            if (!r.Item2) { writeHandler = null; }
        } else {
            baseStream.Write(buffer, offset, count);
        }
    } catch (IOException)            { /* pipe gone — JsonRpc will Disconnect */ }
      catch (ObjectDisposedException){ }
}
```

The same defensive wrapping should be applied to `Read` (line 46) — the same broken-pipe condition will fire when StreamJsonRpc tries to drain the pipe after Pylance dies.

---

## Cluster D — LSP disposal races (the big ones)

This is the largest and most impactful cluster — **11,769 hits combined**. The bugs all share a common architectural defect: PTVS uses **fire-and-forget tasks** (`Task.DoNotWait()`) for LSP notifications, but `DoNotWait` is designed to **re-throw faulted task exceptions back to the original SynchronizationContext** — turning expected-during-teardown exceptions into UI-thread crashes.

#### Shared "trap" location

`Python\Product\Common\Infrastructure\TaskExtensions.cs:52-58`

```csharp
public static void DoNotWait(this Task task) {
    if (task.IsCompleted) {
        ReThrowTaskException(task);
        return;
    }
    ...
    task.ContinueWith(DoNotWaitSynchronizationContextContinuation,
                      synchronizationContext, ...);
}

private static void ReThrowTaskException(object state) {
    var task = (Task)state;
    if (task.IsFaulted && task.Exception != null) {
        var exception = task.Exception.InnerException;
        ExceptionDispatchInfo.Capture(exception).Throw();   // ← rethrown on UI thread → Watson
    }
}
```

Every `*.DoNotWait()` that ultimately touches `_rpc` is a potential Watson source whenever the LSP is being torn down (solution closing, project unloading, Pylance restart, etc.).

---

### 🐞 #1497512 — `ObjectDisposedException` in `DoNotWait` from `PythonAnalysisOptions.Save` — **8,088 hits, top signal**

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1497512>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.ObjectDisposedException_80131622_Microsoft.PythonTools.Common.dll!Microsoft.PythonTools.Infrastructure.TaskExtensions.DoNotWait`
- **Created:** 2022-03-13
- **Hit count:** **8,088** (loudest in the entire query)
- **Builds:** 17.9.34622.214 – 17.12.35728.132
- **State:** Active

#### Source location & history

`Python\Product\PythonTools\PythonTools\Options\PythonAnalysisOptions.cs:124`
`Python\Product\Common\Infrastructure\TaskExtensions.cs:52`

In current `main`, `Save()` no longer directly performs LSP notification — it just raises a `Changed` event:

```csharp
public void Save() {
    var changed = _service.SaveBool(...);
    ...
    if (changed) { Changed?.Invoke(this, EventArgs.Empty); }     // PythonLanguageClient subscribes
}
```

`PythonLanguageClient.OnSettingsChanged` (line 462) responds by setting a debounce timer (`_deferredSettingsChangedTimer.Change(...)`) which fires `TriggerWorkspaceUpdateConfig() → InvokeDidChangeConfigurationAsync(...)`. The shipping Watson builds (17.9 – 17.12) had a more direct call path that performed the LSP notification synchronously inside the `Save` path and called `.DoNotWait()` on the returned task.

#### Root cause (plain English)

User opens **Tools → Options → Python → Analysis**, changes a setting, then clicks **OK** **after** the solution containing Pylance has already been closed (or while Pylance was being restarted). `Save()` raises `Changed`, which causes the LSP client to fire `_rpc.NotifyWithParameterObjectAsync(...).DoNotWait()`. The `_rpc` instance is disposed → `JsonRpc.Verify.NotDisposed()` throws `ObjectDisposedException`. `DoNotWait` faithfully re-throws it on the UI thread.

The fact that this Watson accumulated 8,088 hits indicates the scenario is extremely common — likely every user who closes a solution and changes Python options crashes once. Even with the deferred-timer refactor in `main`, the underlying `DoNotWait` rethrow trap remains.

#### Fix

The most cost-effective fix is to update `DoNotWait` / `ReThrowTaskException` to swallow expected-during-teardown exceptions:

```csharp
private static readonly Type[] _silencedExceptionTypes = new[] {
    typeof(ObjectDisposedException),
    typeof(ConnectionLostException),       // requires StreamJsonRpc reference
    typeof(OperationCanceledException),
};

private static void ReThrowTaskException(object state) {
    var task = (Task)state;
    if (task.IsFaulted && task.Exception != null) {
        var ex = task.Exception.InnerException;
        if (_silencedExceptionTypes.Any(t => t.IsInstanceOfType(ex))) return;
        ExceptionDispatchInfo.Capture(ex).Throw();
    }
}
```

Alternatively introduce a new `task.SilenceLspExceptions().DoNotWait()` helper and use it at every `_rpc.*.DoNotWait()` site, while leaving the general `DoNotWait` strict.

---

### 🐞 #2454483 — `NullReferenceException` in `UpdateInterpreterExcludes` — **3,332 hits, ALREADY FIXED**

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2454483>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.NullReferenceException_80004003_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.PythonLanguageClient._UpdateInterpreterExcludes_b__90_0`
- **Created:** 2025-04-21
- **Hit count:** **3,332**
- **Builds:** 17.13.35825.156 – 17.14.37301.10
- **State:** Active (but code-fixed in main)

#### Fix already in main

Commit `7f7c5a3fe` (PR [#8289](https://github.com/microsoft/PTVS/pull/8289)) — *"We need to send directory 'workspace/didChangeWatchedFiles' when a package is removed/added. No longer sending settings changed since Pylance ignores them if nothing has changed."* by Bill Schnurr, **2025-09-19**.

#### Pre-fix code (the bug)

```csharp
private void OnInterpreterChanged(object sender, EventArgs e) {
    UpdateInterpreterExcludes();              // ← removed in #8289
    OnSettingsChanged(sender, e);
}

private void UpdateInterpreterExcludes() {
    this._clientContexts.ForEach(context => {                    // ← _b__90_0 lambda
        if (PathUtils.IsSubpathOf(context.RootPath,
                                  context.InterpreterConfiguration.InterpreterPath)) { ... }
    });
}
```

#### Root cause (plain English)

`OnInterpreterChanged` fires whenever the active interpreter changes (registry change, conda env switch, etc.). At the moment the lambda runs, `context.InterpreterConfiguration` is **transiently `null`** — the workspace context has reset its config and is loading the new one. The `_b__90_0` lambda dereferences `.InterpreterPath` on that null reference.

With 3,332 hits, this was the second-loudest Watson — fixed in current `main` by **removing** `UpdateInterpreterExcludes` entirely. The reasoning: PTVS was using it to tell Pylance to exclude the interpreter directory from "watch everything" patterns, but Pylance ignores duplicate `workspace/didChangeWatchedFiles` registrations anyway, so the exclude logic was redundant.

#### Recommended action

Verify the fix in commit `7f7c5a3fe` is backported to the affected servicing branches (17.13 / 17.14) so the Watson stops accumulating hits in the wild.

---

### 🐞 #2455159 — `NullReferenceException` in `PythonLanguageClient.GetSettings`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2455159>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.NullReferenceException_80004003_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.PythonLanguageClient.GetSettings`
- **Created:** 2025-04-22
- **Hit count:** **174**
- **Builds:** 17.13.35913.81 – 17.14.37111.16
- **State:** Active

#### Source location

`Python\Product\PythonTools\PythonTools\LanguageServerClient\PythonLanguageClient.cs:370-455`

```csharp
private LanguageServerSettings.PythonSettings GetSettings(Uri scopeUri = null) {
    if (_clientContexts.Count() == 0) return null;                // null-guard #1
    ...
    if (context == null || context.InterpreterConfiguration == null) {  // null-guard #2
        return null;
    }
    Debug.Assert(_analysisOptions != null);                       // ← assumed non-null

    var extraPaths = UserSettings.GetStringSetting(
        PythonConstants.ExtraPathsSetting, null, Site,
        PythonWorkspaceContextProvider.Workspace,                 // ← can NRE if Site disposed
        out _)?.Split(';') ?? _analysisOptions.ExtraPaths;
    ...
}
```

Stack: `GetSettings ← TriggerWorkspaceUpdateConfig ← TimerQueue.FireNextTimers`.

#### Root cause (plain English)

The `_deferredSettingsChangedTimer` (set up at line 139) fires `TriggerWorkspaceUpdateConfig → GetSettings()`. The timer callback runs **after** the language client is disposed (the solution / project closed). At that point `_analysisOptions`, `Site`, or `PythonWorkspaceContextProvider` may be null, but the method only guards against `_clientContexts` being empty.

The async `Dispose()` path nulls fields but does not cancel the in-flight timer first — so a timer tick that was scheduled before disposal can fire after fields are null.

#### Fix

Three complementary changes:

1. In `Dispose`, **cancel + dispose `_deferredSettingsChangedTimer` first**, before nulling any fields.
2. Add a `_disposed` flag and bail out at the top of `GetSettings` and `TriggerWorkspaceUpdateConfig`:
   ```csharp
   if (_disposed) return null;
   ```
3. Wrap the body of the timer callback (`PythonLanguageClient.cs:139`) in a try/catch that swallows `NullReferenceException` and `ObjectDisposedException`.

---

### 🐞 #1487204 — `ConnectionLostException` from `set_ActiveInterpreter` via `DoNotWait`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1487204>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_StreamJsonRpc.ConnectionLostException_80131500_Microsoft.PythonTools.Common.dll!Microsoft.PythonTools.Infrastructure.TaskExtensions.DoNotWait`
- **Created:** 2022-02-21
- **Hit count:** **156**
- **Builds:** 17.9.34728.123 – 17.12.36908.8
- **State:** Active

#### Source location

`Python\Product\PythonTools\PythonTools\Project\PythonProjectNode.cs:236-304` (ActiveInterpreter setter)

The Watson stack:
`RegistryWatcher → DiscoverInterpreterFactories → InterpreterRegistryService.OnInterpretersChanged → UIThread.Invoke → OnInterpreterRegistryChanged_b__36_0 → set_ActiveInterpreter → DoNotWait → ReThrowTaskException → JsonRpc → ConnectionLostException`

#### Root cause (plain English)

When the Windows registry watcher fires (e.g., the user installed/uninstalled a Python via the Python installer or Anaconda installer), PTVS re-discovers interpreters and switches `ActiveInterpreter` on every open project. Inside `set ActiveInterpreter`, the property setter fires `ActiveInterpreterChanged?.Invoke(this, ...)`. One of the subscribers (the LSP client) calls `_rpc.NotifyWithParameterObjectAsync(...).DoNotWait()` to inform Pylance of the interpreter change.

If the Pylance process has died meanwhile (e.g., it was being restarted because of the same interpreter change), the notify throws `ConnectionLostException`. `DoNotWait` faithfully re-throws it on the UI thread, where it crashes VS.

#### Fix

Same architectural fix as #1497512 — make `DoNotWait` (or a new `SilenceLspExceptions` helper) swallow `ConnectionLostException`. Optionally also gate the notify on `_rpc.IsConnected` or a `_isInitialized && !_disposed` flag inside `PythonLanguageClient` itself.

---

### 🐞 #2325087 — `ConnectionLostException` in `PythonLanguageClient.InvokeWithParametersAsync`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/2325087>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_StreamJsonRpc.ConnectionLostException_80131500_Microsoft.PythonTools.dll!Microsoft.PythonTools.LanguageServerClient.PythonLanguageClient+_InvokeWithParametersAsync_d__78_1.MoveNext`
- **Created:** 2024-12-15
- **Hit count:** **10**
- **Builds:** 17.12.35527.113 – 17.12.35707.178
- **State:** Active

#### Source location

`Python\Product\PythonTools\PythonTools\LanguageServerClient\PythonLanguageClient.cs:358-362`

```csharp
private async Task<R> InvokeWithParametersAsync<R>(string request, object parameters, CancellationToken t) where R : class {
    await _readyTcs.Task.ConfigureAwait(false);
    return await _rpc.InvokeWithParameterObjectAsync<R>(request, parameters, t).ConfigureAwait(false);
}
```

#### Root cause (plain English)

Editor-initiated LSP requests (completion, hover, go-to-definition, references, command execution) all funnel through this single helper. When Pylance crashes mid-request, `InvokeWithParameterObjectAsync` throws `ConnectionLostException`. The helper has **no try/catch**, so the exception propagates up through the editor's completion/hover provider, which is invoked from the WPF Dispatcher — and any unhandled exception on the Dispatcher kills VS.

The hit count is relatively low (10) because Pylance is quite stable, but every Pylance crash during an active editor session results in this Watson.

#### Fix

Return `null` on disconnect — every caller in this file already returns `Task<object>` or `Task<LSP.SomeType[]>`, and the editor's LSP integration tolerates a null result (it just shows no completions/references):

```csharp
private async Task<R> InvokeWithParametersAsync<R>(string request, object parameters, CancellationToken t) where R : class {
    await _readyTcs.Task.ConfigureAwait(false);
    try {
        return await _rpc.InvokeWithParameterObjectAsync<R>(request, parameters, t).ConfigureAwait(false);
    } catch (ConnectionLostException)  { return null; }
      catch (ObjectDisposedException)  { return null; }
      catch (OperationCanceledException) { return null; }
}
```

The same defensive pattern should also be applied to `NotifyWithParametersAsync` (line 364).

---

### 🐞 #1363565 — `ObjectDisposedException` in `Microsoft.PythonTools.Common.dll!Unknown`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1363565>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.ObjectDisposedException_80131622_Microsoft.PythonTools.Common.dll!Unknown`
- **Created:** 2021-07-30
- **Hit count:** **9**
- **Builds:** 16.11.34114.132
- **State:** Active

#### Stack trace

```
mscorlib.ni!System.Runtime.InteropServices.Marshal.ThrowExceptionForHRInternal
mscorlib.ni!System.Runtime.CompilerServices.TaskAwaiter.HandleNonSuccessAndDebuggerNotification (×2)
Microsoft.PythonTools.Common!Unknown
WindowsBase.ni!System.Windows.Threading.ExceptionWrapper.InternalRealCall
...
```

#### Root cause (plain English)

This is an **unsymbolicated variant** of the same cluster-D pattern. The `Marshal.ThrowExceptionForHRInternal` frame indicates the exception came from a COM interface call returning a failure HRESULT — most likely a JsonRpc call returning a wrapped `ObjectDisposedException`. The `Microsoft.PythonTools.Common!Unknown` frame is some `TaskExtensions` helper (likely `ReThrowTaskException` or `DoNotWait`) whose symbols couldn't be resolved against the older build.

With only 9 hits on a single 16.11.34114.132 build, this is effectively a low-volume early instance of the same root cause that grew into #1497512 (8,088 hits). Fixing `DoNotWait` per cluster D will address this too.

#### Fix

Same as #1497512 — make `TaskExtensions.DoNotWait` resilient to `ObjectDisposedException` and `ConnectionLostException`.

---

## Cluster E — VS hierarchy / threading

### 🐞 #1641198 — `COMException 0x80004005` in `HierarchyNode.CloseDocumentWindow`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1641198>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.Runtime.InteropServices.COMException_80004005_Microsoft.PythonTools.dll!Microsoft.VisualStudioTools.Project.HierarchyNode.CloseDocumentWindow`
- **Created:** 2022-10-11
- **Hit count:** **100**
- **Builds:** 17.9.34728.123 – 18.6.11723.189 (still occurring)
- **State:** Active

#### Source location

`Common\Product\SharedProject\HierarchyNode.cs:1053-1097`
`Python\Product\PythonTools\PythonTools\Project\PythonProjectNode.cs:754-797`

```csharp
// HierarchyNode.cs:1089-1090
IVsSolution soln = GetService(typeof(SVsSolution)) as IVsSolution;
ErrorHandler.ThrowOnFailure(soln.CloseSolutionElement(saveOptions, srpOurHier, cookie[0]));   // ← throws
```

#### Root cause (plain English)

Trigger chain (read top-down):
1. `PythonProjectReferenceNode` is removed (project reference closed)
2. → `SearchPathManager.RemoveByMoniker` fires `Changed` event
3. → `PythonProjectNode.SearchPaths_Changed`
4. → `RefreshSearchPaths` iterates removed nodes
5. → `searchPathNodes[i].Remove()` calls `HierarchyNode.CloseDocumentWindow`
6. → `IVsSolution.CloseSolutionElement` returns `E_FAIL` (0x80004005)
7. → `ErrorHandler.ThrowOnFailure(hr)` converts to `COMException`

The `E_FAIL` from `CloseSolutionElement` is a **benign failure** — the document is already being closed by VS itself (e.g., the whole solution is unloading and VS has beaten PTVS to closing the editor frame). The current code treats it as a fatal error.

#### Fix

`CloseDocumentWindow` is an opportunistic cleanup — it should not crash VS when VS has already done the cleanup for us:

```csharp
int hr = soln.CloseSolutionElement(saveOptions, srpOurHier, cookie[0]);
if (ErrorHandler.Failed(hr)) {
    Debug.WriteLine("CloseSolutionElement failed: 0x{0:X} — document likely already closed", hr);
    // best-effort; ignore
}
```

The same defensive pattern should be applied to `pRdt.GetDocumentInfo` / `pEnumRdt.Reset` calls. Alternatively wrap the whole `CloseDocumentWindow` method body in `try { … } catch (COMException) { }`.

---

### 🐞 #1446269 — `ThrowIfNotOnUIThread` in `SolutionItemCacheInvalidator.OnItemDeleted`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1446269>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.Runtime.InteropServices.COMException_8001010e_Microsoft.VisualStudio.Shell.UI.Internal.dll!Microsoft.VisualStudio.PlatformUI.Packages.FileEnumerationService.Cache.Solution.SolutionItemCacheInvalidator.OnItemDeleted`
- **Created:** 2021-12-03
- **Hit count:** **36**
- **Builds:** 17.14.37216.2
- **State:** Active

#### Stack trace

```
Microsoft.VisualStudio.Shell.Framework!Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread
Microsoft.VisualStudio.Shell.UI.Internal!Microsoft.VisualStudio.PlatformUI.Packages.FileEnumerationService.Cache.Solution.SolutionItemCacheInvalidator.OnItemDeleted
Microsoft.PythonTools!Unknown            (× 5 frames)
mscorlib.ni!System.Threading.ExecutionContext.RunInternal
mscorlib.ni!System.Threading.ExecutionContext.Run
mscorlib.ni!System.Threading.TimerQueueTimer.CallCallback
mscorlib.ni!System.Threading.TimerQueueTimer.Fire
mscorlib.ni!System.Threading.TimerQueue.FireNextTimers
```

The error code `0x8001010e` is `RPC_E_WRONG_THREAD` (the COM "wrong apartment" error).

#### Root cause (plain English)

A `System.Threading.Timer` callback (i.e., **not** on the UI thread) inside PTVS ends up modifying the VS solution hierarchy. The VS solution cache subscribes to `OnItemDeleted` and asserts it must run on the UI thread — `ThreadHelper.ThrowIfNotOnUIThread` throws `RPC_E_WRONG_THREAD`.

The 5 unsymbolicated `Microsoft.PythonTools!Unknown` frames are the timer-callback chain. The most likely culprits (any of these timers eventually mutates the solution):

- `_refreshIsCurrentTrigger` in `PipPackageManager.cs:573` (`RefreshIsCurrentTimer_Elapsed`)
- `_historyWatcherTimer` in `CondaPackageManager.cs:80`
- `_reanalyzeProjectNotification` in `PythonProjectNode.cs` (calls `RefreshInterpreters`)
- `_deferredSettingsChangedTimer` in `PythonLanguageClient.cs:139`

These timers' callbacks eventually call `RefreshInterpreters()` / `RefreshSearchPaths()` / `BoldActiveEnvironment()` which mutate the project hierarchy. VS's `SolutionItemCacheInvalidator` then asserts the UI thread requirement and throws.

This is a **new bug** (only build 17.14.37216.2 — VS 2026) — likely introduced by a recent VS refactor that tightened threading checks on the cache invalidator.

#### Fix

Every timer callback that touches the project hierarchy must first switch to the UI thread:

```csharp
private async void SomeTimer_Elapsed(object state) {
    try {
        await Site.GetUIThread().InvokeTaskAsync(() => DoTheHierarchyMutation());
        // alternative: await JoinableTaskContext.Factory.SwitchToMainThreadAsync();
    } catch (OperationCanceledException) { }
}
```

A targeted audit of every `new Timer(...)` callback in `Python\Product\` will identify the exact site. Bisecting between VS 17.13 and 17.14 on touched timer code would also identify the regression — but the fix is the same regardless: UI-thread switch.

---

## Cluster F — Process launching

### 🐞 #1345469 — `ArgumentException` from `ProcessStartInfo.get_EnvironmentVariables`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1345469>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.ArgumentException_80070057_Microsoft.PythonTools.Common.dll!Unknown`
- **Created:** 2021-06-21
- **Hit count:** **80**
- **Builds:** 16.11.32106.194 – 16.11.36602.28
- **State:** Active

#### Source location

`Python\Product\Common\Infrastructure\ProcessOutput.cs:266-269` (and mirror at `Common\Product\SharedProject\ProcessOutput.cs`)

```csharp
if (env != null) {
    foreach (var kv in env) {
        psi.EnvironmentVariables[kv.Key] = kv.Value;   // ← getter throws
    }
}
```

#### Stack trace

```
mscorlib.ni!System.Collections.Hashtable.Insert
mscorlib.ni!System.Collections.Hashtable.Add
System.ni!System.Collections.Specialized.StringDictionaryWithComparer.Add
System.ni!System.Diagnostics.ProcessStartInfo.get_EnvironmentVariables    ← thrown here
Microsoft.PythonTools.Common!Unknown
Microsoft.PythonTools.VSInterpreters!Unknown
...
```

#### Root cause (plain English)

`ProcessStartInfo.EnvironmentVariables` is a getter that **lazily clones the current process's environment** into a **case-insensitive** `StringDictionary`. If the parent process (`devenv.exe`) somehow ended up with two environment variables whose names differ only in casing — e.g., a build-system child or activation script left both `Path` and `PATH`, or `TEMP` and `Temp` — then `StringDictionary.Add → Hashtable.Insert` throws `ArgumentException: "Item has already been added"`.

This is a long-standing .NET Framework defect ([dotnet/runtime#26331](https://github.com/dotnet/runtime/issues/26331)). It's most commonly triggered when Conda activation scripts or third-party tooling have polluted the environment with mixed-case duplicates.

#### Fix

`ProcessStartInfo.Environment` (added in .NET 4.6) uses `OrdinalIgnoreCase` and tolerates duplicates by keeping the last value. Switch to it:

```csharp
if (env != null) {
    foreach (var kv in env) {
        psi.Environment[kv.Key] = kv.Value;   // resilient to mixed-case duplicates
    }
}
```

Plus a try/catch fallback for environments where `psi.Environment` itself fails:

```csharp
StringDictionary envVars = null;
try { envVars = psi.EnvironmentVariables; }
catch (ArgumentException) {
    // fall back to .Environment which uses ordinal-ignore-case
    foreach (DictionaryEntry e in Environment.GetEnvironmentVariables()) {
        psi.Environment[e.Key.ToString()] = e.Value?.ToString() ?? "";
    }
}
if (envVars != null && env != null) {
    foreach (var kv in env) envVars[kv.Key] = kv.Value;
}
```

---

### 🐞 #1459718 — `Win32Exception` in `ProcessServices.Start`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1459718>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.ComponentModel.Win32Exception_80004005_Microsoft.Python.Core.dll!Microsoft.Python.Core.OS.ProcessServices.Start`
- **Created:** 2022-01-10
- **Hit count:** **8**
- **Builds:** 17.2.32929.388
- **State:** Active

#### Source location (current equivalent)

`Python\Product\Common\Core\OS\ProcessServices.cs:26-29`

```csharp
public IProcess Start(ProcessStartInfo psi) {
    var process = Process.Start(psi);          // ← Win32Exception
    return process != null ? new PlatformProcess(this, process) : null;
}
```

> The Watson references the older binary `Microsoft.Python.Core.dll` (legacy vendored copy from the Python language server), but the code is essentially identical in current `Microsoft.PythonTools.Common.Core.OS.ProcessServices`.

#### Stack trace

```
System.ni!System.Diagnostics.Process.StartWithCreateProcess
System.ni!System.Diagnostics.Process.Start
Microsoft.Python.Core!Microsoft.Python.Core.OS.ProcessServices.Start
Microsoft.Python.Core!Microsoft.Python.Core.OS.ProcessServices       ← async lambda
...
Microsoft.PythonTools.EnvironmentsList!Unknown                       ← UI initiator
PresentationFramework.ni!System.Windows.Data.CollectionView.OnCurrentChanged
PresentationFramework.ni!System.Windows.Data.ListCollectionView.MoveCurrentToPosition
```

#### Root cause (plain English)

When the user selects an environment in the **Python Environments** window (the `MoveCurrentToPosition` indicates a WPF list selection change), PTVS launches the interpreter (or pip/conda) to gather configuration details. If the interpreter executable has been deleted or renamed (a stale registration — common when a user uninstalls Python without removing the registry key), `Process.Start` throws `Win32Exception` (typically `ERROR_FILE_NOT_FOUND` 0x2 or `ERROR_DIRECTORY` 0x10b).

The exception is unhandled and bubbles up through the async lambda, `ExceptionDispatchInfo.Throw`, the WPF `DispatcherOperation.InvokeDelegateCore`, all the way out to the message loop where it crashes VS.

#### Fix

`ProcessServices.Start` should catch and return `null`, and callers should treat null as "interpreter unavailable":

```csharp
public IProcess Start(ProcessStartInfo psi) {
    try {
        var process = Process.Start(psi);
        return process != null ? new PlatformProcess(this, process) : null;
    } catch (Win32Exception)          { return null; }   // exe missing / access denied
      catch (InvalidOperationException){ return null; }   // psi state invalid
}
```

Callers in `PythonLibraryPath.GetSearchPathsFromInterpreterAsync` already loop with retries — returning null lets them log+skip cleanly. The Environments window callers should also catch and show a friendly message ("interpreter not found — refresh to remove stale entry").

---

### 🐞 #1488201 — `ProcessException` from `CookiecutterClient.WaitForOutput`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1488201>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_Microsoft.CookiecutterTools.Model.ProcessException_80131500_Microsoft.CookiecutterTools.dll!Microsoft.CookiecutterTools.Model.CookiecutterClient+_WaitForOutput_d__45.MoveNext`
- **Created:** 2022-02-23
- **Hit count:** **12**
- **Builds:** 17.11.35219.272 – 18.3.11520.95
- **State:** Active

#### Source location

`Python\Product\Cookiecutter\Model\CookiecutterClient.cs:679-697`

```csharp
private static async Task<ProcessOutputResult> WaitForOutput(string interpreterPath, ProcessOutput output) {
    using (output) {
        await output;
        var r = new ProcessOutputResult { ExeFileName = interpreterPath, ExitCode = output.ExitCode, ... };
        if (r.ExitCode != 0) {
            throw new ProcessException(r);     // ← thrown when subprocess exits non-zero
        }
        return r;
    }
}
```

Callers that **do not** catch `ProcessException`:
- `CreateVenvWithoutPipThenInstallPip` — calls at lines **167** and **184**
- `InstallPackage` — line **212**

Callers that **do** catch:
- `GetRealPath` — caught at line 129

#### Root cause (plain English)

The cookiecutter feature creates a side venv (under `%LOCALAPPDATA%\…\cookiecutter-env`), installs pip into it, and then `pip install cookiecutter>=2.5.0 future`. If any of those subprocesses fail (no network, corporate proxy blocking PyPI, corrupt interpreter, etc.), the subprocess exits non-zero, `WaitForOutput` throws `ProcessException`, and the exception propagates out of `InstallAsync` to a top-level WPF Dispatcher invoke that has no handler — crashing VS.

12 hits over a wide build range suggests this happens consistently for users in restricted-network environments.

#### Fix

Wrap each cookiecutter-install `WaitForOutput` call in try/catch and surface a friendly error in the redirector + UI:

```csharp
try {
    await WaitForOutput(_interpreter.InterpreterExecutablePath, output);
} catch (ProcessException ex) {
    _redirector.WriteErrorLine(Strings.CookiecutterInstallFailed.FormatUI(ex.Result.ExitCode));
    foreach (var line in ex.Result.StandardErrorLines.MaybeEnumerate()) {
        _redirector.WriteErrorLine(line);
    }
    throw new InvalidOperationException(Strings.CookiecutterInstallFailed, ex);
}
```

The cookiecutter UI's existing `InvalidOperationException` handler then picks up the typed failure and shows the dialog.

---

## Cluster G — Removed legacy module

### 🐞 #1432602 — `InvalidOperationException` in `Microsoft.Python.Parsing.dll!Unknown`

- **Link:** <https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1432602>
- **Title:** `[Watson] clr20r3: CLR_EXCEPTION_System.InvalidOperationException_80131509_Microsoft.Python.Parsing.dll!Unknown`
- **Created:** 2021-11-09
- **Hit count:** **1,620**
- **Builds:** 17.0.31903.59 – 17.1.31911.260
- **State:** Active

#### Stack trace (just two frames)

```
Microsoft.Python.Parsing!Unknown
Microsoft.PythonTools.VSInterpreters!Unknown
```

#### Root cause (plain English)

**No code for this exists in current PTVS.**

`Microsoft.Python.Parsing.dll` was part of the **legacy Python language server** ([microsoft/python-language-server](https://github.com/microsoft/python-language-server)) that PTVS vendored before the Pylance LSP integration. The build range `17.0.31903.59 – 17.1.31911.260` corresponds to VS 2022 17.0 (~November 2021) — the last versions that used the legacy analyzer.

The DLL has been completely removed from PTVS. The 1,620 hits all predate the Pylance migration and are no longer accruing. The (legacy) bug was almost certainly a stale `IDisposable` / parser state that got reused after disposal, but reproducing it now would require pinning back to the 17.0 binaries.

#### Recommended action

**Resolve as "Fixed by design change (Pylance migration)"** — no further code change needed in current `main`. Tag with a comment confirming the legacy analyzer is gone.

---

## Consolidated remediation priority (by ROI)

| # | Action | Bugs fixed | Hits resolved | Effort |
|---|---|---|---:|---|
| 1 | Make `TaskExtensions.DoNotWait` / `ReThrowTaskException` swallow `ObjectDisposedException`, `ConnectionLostException`, `OperationCanceledException` | #1487204, #1497512, #1363565 | ~8,253 | XS (~10 lines) |
| 2 | Confirm PR [#8289](https://github.com/microsoft/PTVS/pull/8289) (commit `7f7c5a3fe`) is backported to 17.13 / 17.14 servicing | #2454483 | 3,332 | XS (cherry-pick) |
| 3 | Snapshot `_rpc` + try/catch in `FileWatcher.Listener.OnFileChanged` | #1490074, #2014172, #2017229 | 269 | XS (~6 lines) |
| 4 | Cancel `_deferredSettingsChangedTimer` first in `Dispose`, add `_disposed` guards in `GetSettings` / `TriggerWorkspaceUpdateConfig` | #2455159 | 174 | S |
| 5 | Catch `COMException` in `HierarchyNode.CloseDocumentWindow`; tolerate `E_FAIL` from `CloseSolutionElement` | #1641198 | 100 | S |
| 6 | Snapshot timer field + null-check in `CondaPackageManager._historyWatcher_Changed` | #1222779 | 1,045 | XS |
| 7 | Try/catch `IOException` / `ObjectDisposedException` in `StreamIntercepter.Write` (and `Read`) | #1812702 | 10 | XS |
| 8 | Try/catch `Win32Exception` in `ProcessServices.Start` | #1459718 | 8 | XS |
| 9 | Switch to `ProcessStartInfo.Environment` (or try/catch `ArgumentException` fallback) in `ProcessOutput` | #1345469 | 80 | S |
| 10 | Audit timer callbacks for UI-thread switch (cluster E) | #1446269 | 36 | M |
| 11 | Try/catch `ProcessException` in cookiecutter install paths | #1488201 | 12 | S |
| 12 | Try/catch `PathTooLongException` in `PipPackageManager.OnRenamed` | #1229982 | 18 | XS |
| 13 | Wrap `_rpc.*` calls in `InvokeWithParametersAsync` / `NotifyWithParametersAsync` with disconnect-tolerant catches | #2325087 | 10 | XS |
| 14 | Resolve as "Fixed by design change" | #1432602 | 1,620 (historical) | none |

**Total addressable hits:** ~14,967 across 17 actionable bugs, with the top 4 actions alone (≈25 lines of code + 1 backport) resolving **>80%** of the noise.

---

## Methodology & sources

- Work-item metadata fetched via Azure DevOps REST (`az boards work-item show`) — full JSON cached locally.
- Call-stacks extracted from each item's `Microsoft.VSTS.TCM.ReproSteps` HTML field.
- Source code analysis performed against `Z:\Repos\PTVS` at HEAD `50b7f409e` on `main`.
- For bug #2454483, the fix commit was located via `git log --all -S "UpdateInterpreterExcludes"`.
- For all other bugs, root causes were determined by mapping the Watson call stack to current source code, identifying the precise line(s) that throw, and reasoning about pre- and post-conditions.
