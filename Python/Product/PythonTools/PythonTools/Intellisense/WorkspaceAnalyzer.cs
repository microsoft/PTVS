// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Logging;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudioTools;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.PythonTools.Intellisense {
    internal class WorkspaceAnalyzer : IDisposable {
        private readonly IPythonWorkspaceContext _pythonWorkspace;
        private readonly IInterpreterOptionsService _optionsService;
        private readonly IServiceProvider _site;
        private readonly IPythonToolsLogger _logger;

        private IReadOnlyList<IPackageManager> _activePackageManagers;
        private readonly Timer _deferredModulesChangeNotification;

        private FileWatcher _workspaceFileWatcher;
        private readonly HashSet<AnalysisEntry> _pendingChanges, _pendingDeletes;
        private readonly Timer _deferredWorkspaceFileChangeNotification;

        private readonly SemaphoreSlim _recreatingAnalyzer;
        private VsProjectAnalyzer _analyzer;
        private int _analyzerAbnormalExitCount;

        public event EventHandler WorkspaceAnalyzerChanged;

        public event EventHandler<AnalyzerChangingEventArgs> WorkspaceAnalyzerChanging;

        public WorkspaceAnalyzer(
            IPythonWorkspaceContext pythonWorkspace,
            IInterpreterOptionsService optionsService,
            IServiceProvider site
        ) {
            _pythonWorkspace = pythonWorkspace ?? throw new ArgumentNullException(nameof(pythonWorkspace));
            _optionsService = optionsService ?? throw new ArgumentNullException(nameof(optionsService));
            _site = site ?? throw new ArgumentNullException(nameof(site));

            _logger = (IPythonToolsLogger)_site.GetService(typeof(IPythonToolsLogger));
            _pendingChanges = new HashSet<AnalysisEntry>();
            _pendingDeletes = new HashSet<AnalysisEntry>();

            _recreatingAnalyzer = new SemaphoreSlim(1);
            _deferredWorkspaceFileChangeNotification = new Timer(OnDeferredWorkspaceFileChanged);
            _deferredModulesChangeNotification = new Timer(OnDeferredModulesChanged);

            _pythonWorkspace.ActiveInterpreterChanged += OnActiveInterpreterChanged;
            _pythonWorkspace.SearchPathsSettingChanged += OnSearchPathsChanged;
        }

        public VsProjectAnalyzer Analyzer => _analyzer;

        public void Dispose() {
            _site.MustBeCalledFromUIThread();

            _deferredModulesChangeNotification.Dispose();
            _deferredWorkspaceFileChangeNotification.Dispose();
            _recreatingAnalyzer.Dispose();

            _pythonWorkspace.ActiveInterpreterChanged -= OnActiveInterpreterChanged;
            _pythonWorkspace.SearchPathsSettingChanged -= OnSearchPathsChanged;

            if (_analyzer != null) {
                _analyzer.ClearAllTasks();

                if (_analyzer.RemoveUser()) {
                    _analyzer.AbnormalAnalysisExit -= OnAnalysisProcessExited;
                    _analyzer.Dispose();
                }

                _analyzer = null;
            }

            UnsubscribePackageManagers();

            var watcher = _workspaceFileWatcher;
            _workspaceFileWatcher = null;
            watcher?.Dispose();
        }

        public async Task InitializeAsync() {
            SubscribePackageManagers();
            await ReanalyzeAsync();
        }

        private void SubscribePackageManagers() {
            _site.MustBeCalledFromUIThread();

            _activePackageManagers = _optionsService.GetPackageManagers(_pythonWorkspace.CurrentFactory).ToArray();
            foreach (var pm in _activePackageManagers) {
                pm.InstalledFilesChanged += OnInstalledFilesChanged;
                pm.EnableNotifications();
            }
        }

        private void UnsubscribePackageManagers() {
            _site.MustBeCalledFromUIThread();

            var oldPms = _activePackageManagers;
            _activePackageManagers = null;

            foreach (var pm in oldPms.MaybeEnumerate()) {
                pm.DisableNotifications();
                pm.InstalledFilesChanged -= OnInstalledFilesChanged;
            }
        }

        private async Task<VsProjectAnalyzer> CreateAnalyzerAsync(IPythonInterpreterFactory factory, string location) {
            _site.MustBeCalledFromUIThread();

            var pythonServices = _site.GetPythonToolsService();
            var res = await VsProjectAnalyzer.CreateForWorkspaceAsync(
                pythonServices.EditorServices,
                factory,
                location
            );

            res.AbnormalAnalysisExit += OnAnalysisProcessExited;

            await UpdateAnalyzerSearchPathsAsync(res);
            return res;
        }

        private async Task UpdateAnalyzerSearchPathsAsync(VsProjectAnalyzer analyzer) {
            _site.MustBeCalledFromUIThread();

            var workspace = _pythonWorkspace;
            analyzer = analyzer ?? _analyzer;
            if (workspace != null && analyzer != null) {
                await analyzer.SetSearchPathsAsync(workspace.GetAbsoluteSearchPaths());
            }
        }

        private void OnWorkspaceFileDeleted(object sender, FileSystemEventArgs e) {
            var entry = _analyzer?.GetAnalysisEntryFromPath(e.FullPath);
            if (entry != null) {
                lock (_pendingChanges) {
                    _pendingDeletes.Add(entry);
                    try {
                        _deferredWorkspaceFileChangeNotification.Change(500, Timeout.Infinite);
                    } catch (ObjectDisposedException) {
                    }
                }
            }
        }

        private void OnWorkspaceFileChanged(object sender, FileSystemEventArgs e) {
            var entry = _analyzer?.GetAnalysisEntryFromPath(e.FullPath);
            if (entry != null) {
                lock (_pendingChanges) {
                    _pendingChanges.Add(entry);
                    try {
                        _deferredWorkspaceFileChangeNotification.Change(500, Timeout.Infinite);
                    } catch (ObjectDisposedException) {
                    }
                }
            }
        }

        private void OnDeferredWorkspaceFileChanged(object state) {
            var analyzer = _analyzer;
            Uri[] changed, deleted;

            lock (_pendingChanges) {
                if (analyzer == null) {
                    return;
                }
                changed = _pendingChanges.Concat(_pendingDeletes.Where(e => File.Exists(e.Path))).Select(e => e.DocumentUri).ToArray();
                deleted = _pendingDeletes.Where(e => !File.Exists(e.Path)).Select(e => e.DocumentUri).ToArray();
                _pendingChanges.Clear();
                _pendingDeletes.Clear();
            }

            analyzer.NotifyFileChangesAsync(Enumerable.Empty<Uri>(), deleted, changed)
                .HandleAllExceptions(_site, GetType())
                .DoNotWait();
        }

        private void OnDeferredModulesChanged(object state) {
            _site.GetUIThread().InvokeTask(async () => {
                if (_analyzer != null) {
                    await _analyzer.NotifyModulesChangedAsync().ConfigureAwait(false);
                }
            }).HandleAllExceptions(_site).DoNotWait();
        }

        private async Task ReanalyzeAsync() {
            var factory = _pythonWorkspace.CurrentFactory;
            await _site.GetUIThread().InvokeTask(async () => {
                await ReanalyzeWorkspaceAsync(factory);
            });
        }

        private async Task ReanalyzeWorkspaceAsync(IPythonInterpreterFactory factory) {
            _site.MustBeCalledFromUIThread();

#if DEBUG
            var output = OutputWindowRedirector.GetGeneral(_site);
            await ReanalyzeWorkspaceHelperAsync(factory, output);
#else
            await ReanalyzeWorkspaceHelperAsync(factory, null);
#endif
        }

        private async Task ReanalyzeWorkspaceHelperAsync(IPythonInterpreterFactory factory, Redirector log) {
            _site.MustBeCalledFromUIThread();

            try {
                if (!_recreatingAnalyzer.Wait(0)) {
                    // Someone else is recreating, so wait for them to finish and return
                    log?.WriteLine("Waiting for existing call");
                    await _recreatingAnalyzer.WaitAsync();
                    try {
                        log?.WriteLine("Existing call complete");
                    } catch {
                        _recreatingAnalyzer.Release();
                        throw;
                    }
                    if (_analyzer?.InterpreterFactory == factory) {
                        _recreatingAnalyzer.Release();
                        return;
                    }
                }
            } catch (ObjectDisposedException) {
                return;
            }

            IVsStatusbar statusBar = null;
            bool statusBarConfigured = false;
            try {
                if ((statusBar = _site.GetService(typeof(SVsStatusbar)) as IVsStatusbar) != null) {
                    statusBar.SetText(Strings.AnalyzingProject);
                    try {
                        object index = (short)0;
                        statusBar.Animation(1, ref index);
                    } catch (ArgumentNullException) {
                        // Issue in status bar implementation
                        // https://github.com/Microsoft/PTVS/issues/3064
                        // Silently suppress since animation is not critical.
                    }
                    statusBar.FreezeOutput(1);
                    statusBarConfigured = true;
                }

                var oldWatcher = _workspaceFileWatcher;
                _workspaceFileWatcher = new FileWatcher(_pythonWorkspace.Location) {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
                };
                _workspaceFileWatcher.Changed += OnWorkspaceFileChanged;
                _workspaceFileWatcher.Deleted += OnWorkspaceFileDeleted;
                oldWatcher?.Dispose();

                log?.WriteLine("Creating new workspace analyzer");
                var analyzer = await CreateAnalyzerAsync(factory, _pythonWorkspace.Location);
                Debug.Assert(analyzer != null);
                log?.WriteLine($"Created workspace analyzer {analyzer}");

                WorkspaceAnalyzerChanging?.Invoke(this, new AnalyzerChangingEventArgs(_analyzer, analyzer));

                var oldAnalyzer = Interlocked.Exchange(ref _analyzer, analyzer);

                if (oldAnalyzer != null) {
                    if (analyzer != null) {
                        int beforeCount = analyzer.Files.Count();
                        log?.WriteLine($"Transferring from old analyzer {oldAnalyzer}, which has {oldAnalyzer.Files.Count()} files");
                        await analyzer.TransferFromOldAnalyzer(oldAnalyzer);
                        log?.WriteLine($"Tranferred {analyzer.Files.Count() - beforeCount} files");
                        log?.WriteLine($"Old analyzer now has {oldAnalyzer.Files.Count()} files");
                    }
                    if (oldAnalyzer.RemoveUser()) {
                        log?.WriteLine("Disposing old analyzer");
                        oldAnalyzer.Dispose();
                    }
                }

                var files = new List<string>();
                var pythonServices = _site.GetPythonToolsService();
                foreach (var existing in pythonServices.GetActiveSharedAnalyzers().Select(kv => kv.Value).Where(v => !v.IsDisposed)) {
                    foreach (var kv in existing.LoadedFiles) {
                        files.Add(kv.Key);
                        log?.WriteLine($"Unloading {kv.Key} from default analyzer");
                        foreach (var b in (kv.Value.TryGetBufferParser()?.AllBuffers).MaybeEnumerate()) {
                            PythonTextBufferInfo.MarkForReplacement(b);
                        }
                        try {
                            await existing.UnloadFileAsync(kv.Value);
                        } catch (ObjectDisposedException) {
                            break;
                        }
                    }
                }

                if (analyzer != null) {
                    // Set search paths first, as it will save full reanalysis later
                    log?.WriteLine("Setting search paths");
                    await analyzer.SetSearchPathsAsync(_pythonWorkspace.GetAbsoluteSearchPaths());

                    // Add all our files into our analyzer
                    log?.WriteLine($"Adding {files.Count} files");
                    await analyzer.AnalyzeFileAsync(files.ToArray());
                }

                WorkspaceAnalyzerChanged?.Invoke(this, EventArgs.Empty);
            } catch (ObjectDisposedException) {
                // Raced with disposal
            } catch (Exception ex) {
                log?.WriteErrorLine(ex.ToString());
                throw;
            } finally {
                try {
                    if (statusBar != null && statusBarConfigured) {
                        statusBar.FreezeOutput(0);
                        object index = (short)0;
                        statusBar.Animation(0, ref index);
                        statusBar.Clear();
                    }
                } finally {
                    try {
                        _recreatingAnalyzer.Release();
                    } catch (ObjectDisposedException) {
                    }
                }
            }
        }

        private void OnAnalysisProcessExited(object sender, AbnormalAnalysisExitEventArgs e) {
            _analyzerAbnormalExitCount++;

            if (_logger == null) {
                return;
            }

            var msg = new StringBuilder()
                .AppendFormat("Exit Code: {0}", e.ExitCode)
                .AppendLine()
                .AppendLine(" ------ STD ERR ------ ")
                .Append(e.StdErr)
                .AppendLine(" ------ END STD ERR ------ ");
            _logger.LogEvent(
                PythonLogEvent.AnalysisExitedAbnormally,
                msg.ToString()
            );

            if (_analyzerAbnormalExitCount < 5) {
                // Start a new analyzer
                ReanalyzeAsync().HandleAllExceptions(_site).DoNotWait();
            }
        }

        private void OnInstalledFilesChanged(object sender, EventArgs e) {
            try {
                _deferredModulesChangeNotification.Change(500, Timeout.Infinite);
            } catch (ObjectDisposedException) {
            }
        }

        private void OnSearchPathsChanged(object sender, EventArgs e) {
            _site.GetUIThread().InvokeTask(async () => {
                await UpdateAnalyzerSearchPathsAsync(_analyzer);
            }).HandleAllExceptions(_site).DoNotWait();
        }

        private void OnActiveInterpreterChanged(object sender, EventArgs e) {
            _site.GetUIThread().InvokeTask(async () => {
                await OnActiveInterpreterChangedAsync();
            }).HandleAllExceptions(_site).DoNotWait();
        }

        private async Task OnActiveInterpreterChangedAsync() {
            UnsubscribePackageManagers();
            await ReanalyzeAsync();
            SubscribePackageManagers();
        }
    }
}
