# PTVS Unified Settings implementation plan

## 1. Purpose and status

Convert all eight legacy PTVS/Cookiecutter options pages to native Visual Studio
Unified Settings (USX), covering 40 visible settings. Preserve the existing
runtime behavior, project/workspace overrides, automation entry points, and
related hidden preferences.

**Status: implementation in progress, not ready for deployment.** This document
records the approved design and the work required to complete and verify it; it
is not a claim that the migration has passed integration testing.

The original baseline build succeeded using Visual Studio 2026 Enterprise after
the required C++ Spectre-mitigated libraries were installed. The SDK dependency
mismatches have since been resolved by aligning Threading, RPC contracts, and
interop dependencies. PythonTools, Core VSIX, Cookiecutter, and the affected test
projects have built successfully. Both manifests have passed strict validation.
After excluding the three hidden preferences, the latest targeted run passes
28 tests (20 Python tests and eight Cookiecutter tests). The three new regression
cases verify legacy-only reads/writes, no native definitions, no native commits,
and no migration checkpoints for the excluded preferences.

The rebuilt Core VSIX contains 37 visible Python settings and the freshly built
assembly; all 114 packaged localized references resolve. Cookiecutter contributes
three more visible settings. No VSIX installation was attempted for this scope
correction; the user has prohibited installation. Installed-VS behavior remains
unverified. The separate Parameter Information migration-mode conflict described
in section 7 also remains outstanding.

## 2. Approved decisions and boundaries

1. **Native USX is authoritative when USX is enabled.** Runtime reads and writes
   must use native settings, rather than continuously copying between stores.
2. **Perform a one-time programmatic import of legacy values.** This replaces
   the original request for JSON-only, full bidirectional migration. The design
   follows the general direction of [dotnet/fsharp#20877](https://github.com/dotnet/fsharp/pull/20877),
   without copying its independent roaming or post-roaming retry behavior.
3. **Do not overwrite existing USX customizations**, including values explicitly
   customized to the current default. Determine customization from provenance,
   not by comparing the effective value with the default.
4. **No external-settings feature has been approved.** Do not introduce external
   settings providers, external enum providers, or external UI regions. Stop and
   request approval if any becomes necessary.
5. **No independent roaming configuration.** Do not add `roamingOnly` rules or a
   retry after legacy roaming completes.
6. **Legacy import/export compatibility is out of scope**, including the
   Cookiecutter profile contract. Do not retain it by adding synchronization.
7. **Use a fixed formatter list:** `black`, `autopep8`, `yapf`; default `black`.
8. **Target Visual Studio 2026 / 18.x only** for the converted shipping packages.
   Retain the existing .NET Framework 4.7.2 target.
9. **Approved default/reset decisions:** Type Checking is Off; prompting before
   running with build errors is On.
10. Keep legacy data for recovery. Do not automatically delete it or write native
	changes back into it.
11. Preserve operational/security state separately. In particular, do not expose
	trusted workspace locations as ordinary settings or migrate trust decisions
	into a general-purpose settings file.
12. Stop and ask the user if a genuine blocker requires changing these decisions,
	introducing an external-settings feature, or bypassing required validation.
13. Migrate only preferences exposed by the legacy options pages. The three
	hidden Python preferences remain exclusively in legacy storage, with their
	existing readers, writers, and defaults unchanged.
14. Do not install the VSIX. Limit current validation to builds, automated tests,
	and offline package inspection unless the user changes that instruction.

## 3. Setting inventory and target defaults

All Python moniker suffixes below have the prefix `languages.python.`. Cookiecutter
monikers have the prefix `cookiecutter.general.`. These names are the contract
between the native manifests, runtime adapters, importer, and tests.

Empty path/string defaults are native empty strings, rather than nullable JSON
values. Analysis path lists are native arrays of strings, defaulting to `[]`.

### 3.1 Python Tools: General -- six visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 1 | Show output for virtual environment creation | `general.showOutputWindowForVirtualEnvCreate` | Boolean | `true` |
| 2 | Show output for package installation | `general.showOutputWindowForPackageInstallation` | Boolean | `true` |
| 3 | Prompt to create an environment | `general.promptForEnvCreate` | Boolean | `true` |
| 4 | Prompt to install packages | `general.promptForPackageInstallation` | Boolean | `true` |
| 5 | Prompt about test framework configuration | `general.promptForTestFrameWorkInfoBar` | Boolean | `true` |
| 6 | Elevate pip | `general.elevatePip` | Boolean | `false` |

The hidden unsupported-Python-version prompt is not part of the USX conversion.
Keep its legacy `Advanced` category and
`PromptForPythonVersionNotSupportedInfoBarSetting` key, default `true`. Do not
define, import, or route this preference through a native setting.

### 3.2 Python Tools: Analysis -- 12 visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 7 | Auto search paths | `analysis.autoSearchPaths` | Boolean | `true` |
| 8 | Diagnostic mode | `analysis.diagnosticMode` | String enum | `openFilesOnly` |
| 9 | Log level | `analysis.logLevel` | String enum | `Information` |
| 10 | Stub path | `analysis.stubPath` | Path string | Empty |
| 11 | Type checking mode | `analysis.typeCheckingMode` | String enum | `off` |
| 12 | Typeshed paths | `analysis.typeshedPaths` | String array | `[]` |
| 13 | Use library code for types | `analysis.useLibraryCodeForTypes` | Boolean | `true` |
| 14 | Extra paths | `analysis.extraPaths` | String array | `[]` |
| 15 | Indexing | `analysis.indexing` | Boolean | `true` |
| 16 | Import format | `analysis.importFormat` | String enum | `absolute` |
| 17 | Variable type inlay hints | `analysis.inlayHintsVariableTypes` | Boolean | `false` |
| 18 | Function return type inlay hints | `analysis.inlayHintsFunctionReturnTypes` | Boolean | `false` |

Retain the supported analysis enum values and their casing. Reset must also clear
Extra Paths; the legacy Reset implementation omitted that property. Existing
project and workspace configuration must continue to override the global values.

### 3.3 Python Tools: Debugging -- 11 visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 19 | Prompt before running with build errors | `debugging.promptBeforeRunningWithBuildError` | Boolean | `true` |
| 20 | Wait on abnormal exit | `debugging.waitOnAbnormalExit` | Boolean | `true` |
| 21 | Wait on normal exit | `debugging.waitOnNormalExit` | Boolean | `true` |
| 22 | Tee standard output | `debugging.teeStandardOutput` | Boolean | `true` |
| 23 | Break on SystemExit with zero status | `debugging.breakOnSystemExitZero` | Boolean | `false` |
| 24 | Debug the standard library | `debugging.debugStdLib` | Boolean | `false` |
| 25 | Show function return values | `debugging.showFunctionReturnValue` | Boolean | `true` |
| 26 | Class variable presentation | `debugging.variablePresentationForClasses` | String enum | `Group` |
| 27 | Function variable presentation | `debugging.variablePresentationForFunctions` | String enum | `Group` |
| 28 | Protected variable presentation | `debugging.variablePresentationForProtected` | String enum | `Inline` |
| 29 | Special variable presentation | `debugging.variablePresentationForSpecial` | String enum | `Group` |

Presentation values are `Group`, `Hide`, and `Inline`, preserving the existing
runtime enum names. The build-error prompt uses the inverse of the legacy
`DontPromptBeforeRunningWithBuildError` value. Other renamed legacy keys include
`TeeStandardOut` and `ShowReturnValue`.

### 3.4 Python Tools: Conda -- one visible setting

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 30 | Custom Conda executable | `conda.customCondaExecutablePath` | Executable path string | Empty |

### 3.5 Python Tools: Interactive Windows -- two visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 31 | Use smart history | `interactive.useSmartHistory` | Boolean | `true` |
| 32 | Startup scripts location | `interactive.scripts` | Path string | Empty |

The separate hidden debug-interactive preferences are not part of the USX
conversion. Keep `UseSmartHistory` and `Scripts` in the legacy
`Debug Interactive Window` category, with the same defaults and existing runtime
behavior. They must not acquire native definitions or migration checkpoints.

### 3.6 Python editor: Advanced -- three visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 33 | Complete function parentheses | `advanced.completeFunctionParens` | Boolean | `false` |
| 34 | Auto-import completions | `advanced.autoImportCompletions` | Boolean | `true` |
| 35 | Parameter Information | `general.showCompletionOnTyping.parameterInfo` | Boolean | `true` |

Parameter Information means automatic signature help while typing a function
call. It is shell-owned, unlike the two other settings on this page. Its special
compatibility requirements are described in section 7.

### 3.7 Python editor: Formatting -- two visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 36 | Remove REPL prompts when pasting | `formatting.pasteRemovesReplPrompts` | Boolean | `true` |
| 37 | Formatter | `formatting.formatter` | String enum | `black` |

The fixed enum contains exactly `black`, `autopep8`, and `yapf`, the formatter
exports present in the repository. Do not introduce dynamic provider machinery.
Preserve formatter resolution order: project/workspace selection, global setting,
then the existing runtime fallback.

### 3.8 Cookiecutter: General -- three visible settings

| # | Setting | Moniker suffix | Type | Default/reset |
|---|---------|----------------|------|---------------|
| 38 | Show help | `showHelp` | Boolean | `true` |
| 39 | Recommended template feed | `feedUrl` | String | `https://go.microsoft.com/fwlink/?LinkID=823051` |
| 40 | Check for template updates | `checkForTemplateUpdate` | Boolean | `true` |

The complete native inventory is therefore 40 visible definitions: 37 in the
Python manifest (36 Python-owned settings plus Parameter Information) and three
in the Cookiecutter manifest. The three hidden Python preferences, suppressed
dialog values, and trust records remain in their existing legacy storage.

## 4. Existing roaming and import/export behavior

| Legacy settings | Existing behavior established during investigation | Migration decision |
|-----------------|-----------------------------------------------------|--------------------|
| 36 visible Python-owned settings | Stored in the Python user settings collection; no registered legacy roaming contract was found | No independent roaming or post-roaming import |
| Three Cookiecutter settings | No registered legacy roaming contract was found | Same decision |
| Parameter Information | Inherits the shell editor's `TextEditor.*` registration, with both `IsShared` and `IsRoamed` enabled | Use the canonical shell setting; inherited sharing affects the effective migration mode and remains an open design issue |
| Hidden Python preferences | Not exposed by the legacy options pages; retain existing local storage and behavior | Excluded from USX and programmatic import |
| Suppressed dialogs and trusted workspaces | Operational/security state, not ordinary native settings in this conversion | Retain their existing storage and explicit reset action |

The ordinary Python options pages do not register legacy profile import/export
support. Cookiecutter does register profile support, and its three values appear
in the General settings export. The user explicitly accepted ignoring this
compatibility. No migration or synchronization should be added to preserve it.

Local sharing and cloud roaming are separate concerns. Two running VS windows
using the same installation/profile also use the same persisted legacy store,
which does not guarantee immediate refresh of cached options. Separate
installations/profiles do not automatically share the ordinary Python and
Cookiecutter preferences. Parameter Information is the shell-managed exception;
its inherited registration was missed by the earlier Python-specific inventory.
This work adds no independent roaming registration, mirror, or listener.

## 5. Runtime architecture

### 5.1 Shared native settings adapter

Use a small source-linked adapter in the PythonTools and Cookiecutter assemblies.
Do not add Visual Studio settings dependencies to the host-neutral Common
assembly solely to share this code.

The adapter must:

- Obtain the native settings manager and customization metadata services.
- Read typed scalars and arrays with registration/validation checks.
- Report invalid registration, invalid values, unavailable services, and failed
  writes explicitly rather than silently returning a success-shaped default.
- Use `CustomizeUnconditionally` for scalar imports so an explicit legacy value
  equal to the current default remains a customization.
- Use the native array API for arrays; do not serialize native arrays back into
  delimited strings. Account for the public array writer's different options
  surface when testing default-valued arrays.
- Check both enqueue and commit outcomes. Queuing a change is not proof that it
  was committed. Pending approval or failure must not create a success marker.
- Subscribe to relevant effective-value changes and return a disposable
  subscription.
- Distinguish the adapter's own writes from external changes. Do not reload
  cached options halfway through a multi-property Save operation and overwrite
  values that have not yet been saved.

### 5.2 Python runtime cutover

Route known legacy category/key pairs through the native adapter in the existing
options backend. Preserve the public options-service interface and its test
mocks. Keep unmapped operational state on its existing legacy path.

The old Python storage is `PythonTools\Options\<category>` in the user settings
store. General, Debugging, and Advanced editor options share the `Advanced`
category; they must not be separated based on their UI page names during import.

Use explicit typed array access for Typeshed Paths and Extra Paths through the
service wrappers. Otherwise a semicolon in a native array element could be split
by the legacy string parser on every read.

Import before initializing option consumers. Subscribe to native changes and
reload only already-created option caches for the affected category. Dispatch
runtime cache updates to the UI thread, retain existing change events, and
dispose subscriptions with the owning service. Do not materialize every lazy
options object just to process one setting change.

Verify all consumers and writers, including automation, debugger configuration,
language-server configuration, environment prompts, Conda selection, interactive
windows, and formatting. No writer should continue updating a migrated legacy
property while runtime reads use USX.

### 5.3 Cookiecutter runtime cutover

Cookiecutter has a separate package and legacy DialogPage storage mechanism.
Migrate its values independently; do not assume loading PythonTools initializes
Cookiecutter or vice versa.

Take a legacy storage snapshot before switching the page properties to native
reads/writes. The native runtime facade must supply the effective feed URL, help
preference, and update preference without relying on stale page backing fields.

Preserve Apply/Cancel semantics if the legacy page remains available in classic
Options: editing a control must not commit a native value before Apply. Avoid
writing unchanged snapshot properties over newer native values.

### 5.4 Classic Options mode

When VS explicitly reports that native settings are unavailable because USX is
disabled, retain the legacy path for classic Options. Do not run imports or
advance native migration markers in that mode. This is mode-specific behavior,
not continuous bidirectional synchronization.

Do not use legacy fallback to hide missing native registrations or other errors
while USX is enabled. Validate switching modes and restarting VS explicitly.

## 6. One-time importer algorithm

Use versioned, per-setting checkpoints stored locally with the package's legacy
state. A Python checkpoint namespace is
`PythonTools\Options\UnifiedSettingsMigration\v1`. Cookiecutter uses an equivalent
package-local checkpoint namespace.

For each registered setting:

1. If its successful-import checkpoint exists, do not read or write legacy data.
2. Ask native metadata whether the setting is already customized. Protect both
   default-scope customizations and an effective user customization in another
   scope. A customized-to-default value is still a customization.
3. If already customized, preserve it and mark this setting's import complete.
4. Otherwise, read the actual legacy source without going through the new native
   adapter. Distinguish absent values from present values equal to a default.
5. If the legacy value is absent, mark that setting complete without writing a
   synthetic native customization. Do not revisit it after later legacy changes.
6. Convert present legacy data to the registered native type:
   - Parse string-backed Booleans explicitly.
   - Invert the legacy build-error prompt key.
   - Preserve valid enum values and their expected casing.
   - Split legacy analysis path strings using the existing semicolon/CR/LF
	 behavior once, removing empty entries.
   - Normalize supported empty string/path representations.
   - Convert Parameter Information's numeric value as described below.
7. Enqueue and commit the typed native value. Check the actual outcome.
8. Write the success checkpoint only after a successful commit or a confirmed
   terminal skip. Never checkpoint an invalid value, an unknown provenance, a
   rejected write, or a request still awaiting approval.
9. Log a noncritical per-setting failure with identifying context. Leave its
   checkpoint unset so the next package/service initialization can retry it.
   Successful settings must not be replayed because another setting failed.

The algorithm must be crash-safe and idempotent. If native commit succeeds but
checkpointing is interrupted, the next run sees the native customization and
does not overwrite it. There is no global all-or-nothing completion flag that
would prevent retrying incomplete work.

There is no retry triggered by roaming completion, no periodic import, no
reverse synchronization, and no removal of legacy source data.

## 7. Parameter Information compatibility bridge

Use the canonical Boolean moniker:

`languages.python.general.showCompletionOnTyping.parameterInfo`

Keep its UI placement with the Python Advanced editor settings. Do not change
the language registration's `ShowCompletion = false` value merely to force
generation of this one setting. Current shell source omits the generated
completion category for Python because of that registration, so the manifest
must register this setting explicitly.

The existing text manager reads and writes the canonical setting using
`ApplyMigration` and legacy numeric values. A plain Boolean definition without a
migration transform is therefore insufficient.

Register a JSON **`apiOnly`** transform:

- Legacy input: `TextEditor.Python.Auto List Params`.
- Numeric `0` maps to Boolean `false`.
- Numeric `1` maps to Boolean `true`.
- Reverse API conversion supplies the corresponding numeric value to the text
  manager.

The declared intent is an API type-conversion bridge with a programmatic initial
import. However, the VS source promotes migration rules for shared/roamed legacy
locations to effective `FullWithoutDefaults`, including custom `apiOnly` rules.
Because `TextEditor.Python.Auto List Params` inherits `TextEditor.*`, the current
declaration cannot be described as API-only/no synchronization. Resolving this
conflict with the approved one-time-import design remains outstanding; no change
to that bridge is part of the hidden-preference exclusion.

Read the actual historical persisted value, not an already-defaulted text-manager
cache, and reject unexpected numeric values explicitly.

Preserve the text-manager preference change notifications and the existing
signature-help consumers. Validate the complete path in installed VS: native UI
change, text-manager preference update, cached language preference update, and
actual signature-help behavior. Confirm that there is no duplicate registration
and that no inherited roaming metadata changes the intended `apiOnly` behavior.

## 8. Manifest, localization, and UI work

- Define all 40 visible native properties with types, defaults, ordering, localized
  titles/descriptions, and localized enum labels.
- Use native path and array editors supported by the selected SDK schema.
- Disable transient USX scopes for owned settings so existing explicit
  project/workspace overrides remain the effective configuration mechanism.
- Exclude the three hidden preferences from native definitions, mappings,
  localization, and notifications; preserve their legacy persistence paths.
- Strict validation must use the selected manifest SDK, which does not accept
  every field in the latest VS schema.
- Reuse existing resources where practical; add localized resource entries when
  needed. Validate embedded resource base names and assembly references.
- Blend with existing generated Python categories without duplicate shell
  definitions. Use explicit placement for Parameter Information.
- Mark all eight converted legacy pages as represented in Unified Settings,
  while retaining classic-mode behavior where required.
- Preserve the General page's reset-suppressed-dialogs action as a native command.
  Register its command ID, command-table entry, package handler, and native UI
  placement consistently. The handler must reset the existing suppressed-dialog
  values and clear trusted workspaces, matching the old button's behavior.
- Do not resurrect disabled/obsolete options while enumerating old properties.

## 9. SDK, build, and packaging work

1. Select VS 18.x settings API packages that include provenance metadata,
   `CustomizeUnconditionally`, and converted-page registration support. The
   current implementation uses 18.0.42423 for Utilities, Shell.15.0,
   Shell.Framework, and Internal Shell.Framework.
2. Align their required dependencies, especially Microsoft.VisualStudio.Threading,
   with those assemblies. Do not leave a mixed 17.x/18.x reference graph.
3. Use Microsoft.VisualStudio.Settings.Manifest.Build 18.12.1217 for strict
   manifest validation. Import its targets explicitly for the classic projects.
4. Add the verified public SDK/implementation feeds needed for reproducible
   restore. Restore only affected dependencies rather than running a broad
   pre-build script that would also rewrite unrelated npm state.
5. Ensure assembly resolution selects the intended SDK versions ahead of stale
   package copies. Preserve repository package layout and avoid casually
   repointing unrelated versionless links.
6. Register each manifest with its owning package and embed its localized
   resources in that package's actual assembly.
7. Ship the Python manifest in the Core VSIX. PythonTools itself does not create
   the shipping VSIX, so validating only its project is insufficient.
8. Ship the Cookiecutter manifest and resources in the Cookiecutter VSIX.
9. Update the converted shipping VSIX installation/Core Editor prerequisite
   ranges from `[17.0,19.0)` to `[18.0,19.0)`.
10. Inspect built VSIX contents and package registration output. A loose manifest
	beside a development build is not sufficient proof of shipping integration.
11. Verify that the minimum supported installed VS 18.x runtime provides the
	APIs used by the selected SDK. Compilation alone does not establish this.

Use the VS 2026 Enterprise toolchain for validation. A representative targeted
build command is:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe' `
	C:\repos\PTVS\Python\Product\PythonTools\PythonTools.csproj `
	/t:Build /p:Configuration=Debug /p:VSTarget=18.0 /p:VisualStudioVersion=18.0 `
	/m /nologo /v:minimal /clp:ErrorsOnly
```

The current open solution does not contain every affected product project.
Use targeted command-line builds when the Visual Studio build tool reports that
a project is not loaded. Do not bypass native dependencies or Spectre mitigation
to obtain a misleading managed-only success.

## 10. Verification matrix

### 10.1 Deterministic regression tests

Use the existing test projects; do not create a new test framework or unrelated
test harness.

| Area | Required cases |
|------|----------------|
| Inventory | Every visible setting has exactly one expected registration; backend and manifest monikers/types agree; hidden preferences have no native definitions or migration checkpoints and still read/write legacy storage |
| Defaults | Fresh profile and Reset agree, especially Type Checking Off, build-error prompt On, formatter black, and empty path arrays |
| Scalar import | True/false, explicit default value, strings, each supported enum, and the inverted build-error key |
| Missing legacy values | Native default retained; terminal checkpoint prevents later stale import |
| Existing native customization | Nondefault value and explicitly customized default both preserved |
| Provenance failures | Unknown or unavailable provenance does not permit overwriting or checkpointing |
| Commit outcomes | Enqueue failure, commit failure, pending approval, success, and no-op paths; no false success marker |
| Resume behavior | Partial failure, repeat initialization, and a crash window between native commit and checkpoint |
| Arrays | Legacy separator conversion, empty values, ordering, native persistence, and native elements containing semicolons |
| Invalid legacy data | Invalid Boolean/enum/numeric value logs an identifiable failure and leaves retry possible |
| Native notifications | External changes refresh initialized caches and events; unused lazy caches remain uninitialized |
| Own writes | Multi-property Save does not reload and overwrite unsaved properties |
| Disposal | Subscriptions removed; queued notifications do not use disposed services |
| Cookiecutter | Independent startup, preservation of native customization, runtime effective values, Apply/Cancel, and changed-only writes |
| Parameter Information | Numeric 0/1 to Boolean and reverse API conversion; unsupported numeric value rejected |
| Classic mode | Legacy behavior remains available; no native import/checkpoint while USX is disabled |

### 10.2 Static and build validation

- Run strict validation against both final manifests using the restored manifest
  SDK, not only generic JSON schema validation.
- Verify all localized resource references resolve from the built assemblies.
- Build the affected product and test projects with the VS 18 toolchain.
- Run the smallest relevant test set, expanding only where failures justify it.
- Check the Core and Cookiecutter VSIX layouts and registration output.
- Review the diff for unexpected roaming, external providers, legacy writes,
  unsupported APIs, and unrelated changes.

### 10.3 Installed Visual Studio integration

1. Start with a clean experimental profile and verify all native pages, labels,
   editors, enum choices, visibility, and defaults.
2. Start with representative customized legacy profiles and verify one-time
   import before runtime consumers initialize.
3. Prepopulate native customizations, including default-valued customizations,
   and confirm the importer preserves them.
4. Edit native settings and verify the corresponding live runtime behavior
   without restarting where the old feature supported live updates.
5. Restart and confirm persistence, no duplicate imports, and no reverse writes
   to migrated legacy values.
6. Exercise project/workspace overrides, formatter precedence, interactive
   settings, debugger presentation, environment prompts, and Cookiecutter flows.
7. Verify Parameter Information end to end through the shell text manager and
   actual signature help.
8. Exercise Reset and the reset-suppressed-dialogs/trusted-workspaces command.
9. Exercise classic Options mode separately; do not confuse it with USX failure
   fallback.
10. Confirm logs identify failed imports and that a later initialization retries
	only incomplete work.

## 11. Implementation sequence and current progress

| Phase | Deliverable | Current status |
|-------|-------------|----------------|
| A | Inventory, VS source investigation, approvals, and baseline build | Complete |
| B | SDK/feed/build dependency updates | Dependencies aligned; affected product/test builds pass |
| C | Python and Cookiecutter native definitions and localization | 40 visible definitions; three hidden preferences excluded; strict validation and packaged localization checks pass |
| D | Shared typed adapter and one-time importers | Implemented and regression-tested; Parameter Information effective migration mode remains unresolved |
| E | Runtime cutover, native notifications, arrays, and classic-mode handling | Implemented; automated coverage passes; installed-VS behavior unverified |
| F | Converted-page onboarding, native reset action, and VSIX shipping | Registration/reset command wired; Core and Cookiecutter archive contents checked without installation |
| G | Regression tests and targeted builds | Latest targeted run: 28 passed; PythonTools/test build and offline Core VSIX rebuild pass |
| H | Installed-VS behavior and minimum-runtime verification | Not completed; no VSIX installation permitted |
| I | Final review and completion report | Hidden-preference exclusion complete; overall migration pending section 7 and installed-VS verification |

This table is a snapshot accompanying the plan, not an automatically maintained
test report. A later successful build/test report should supersede its status
entries with actual command results and remaining limitations.

## 12. Acceptance criteria

The conversion is complete only when:

- All eight pages and 40 visible settings are available as native settings.
- The three hidden Python preferences retain legacy persistence and behavior,
  with no native definitions, import, or migration checkpoints.
- No external-settings feature was used without explicit approval.
- Existing USX customizations survive migration, including customized defaults.
- Legacy import is one-time, resumable, logged, and checkpointed only after a
  confirmed terminal outcome.
- Runtime reads/writes and live notifications are consistently native when USX
  is enabled, with no continuous legacy synchronization.
- No independent roaming rules or post-roaming retry were introduced.
- Import/export compatibility was not accidentally reintroduced as a new
  requirement.
- Approved default/reset values and existing project/workspace precedence hold.
- Parameter Information works through the shell's numeric API contract and the
  canonical Boolean setting.
- The reset-suppressed-dialogs action preserves its prior behavior without
  exposing trust records as ordinary settings.
- Strict manifest validation, targeted builds/tests, shipping package inspection,
  and required installed-VS checks have passed or a specific blocker has been
  reported to the user for a decision.
- Unrelated existing worktree changes are preserved. No commit is created unless
  separately requested.