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
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Analysis.LanguageServer;
using Newtonsoft.Json.Linq;
using StreamJsonRpc;

namespace Microsoft.Python.LanguageServer.Implementation {
    public partial class LanguageServer {
        private InitializeParams _initParams;
        private bool _shutdown;
        private Action<Task, object> DisposeStateContinuation { get; } = (t, o) => ((IDisposable) o).Dispose();

        [JsonRpcMethod("initialize")]
        public async Task<InitializeResult> Initialize(JToken token, CancellationToken cancellationToken) {
            _initParams = token.ToObject<InitializeParams>();
            MonitorParentProcess(_initParams);
            var priorityToken = await _prioritizer.InitializePriorityAsync(cancellationToken);
            if (_initParams.initializationOptions.asyncStartup) {
                _server.Initialize(_initParams, cancellationToken)
                    .ContinueWith(DisposeStateContinuation, priorityToken, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default)
                    .DoNotWait();
                return _server.GetInitializeResult();
            }

            try {
                return await _server.Initialize(_initParams, cancellationToken);
            } finally {
                priorityToken.Dispose();
            }
        }

        [JsonRpcMethod("initialized")]
        public async Task Initialized(JToken token) { 
            await _server.Initialized(token.ToObject<InitializedParams>());
            _rpc.NotifyAsync("python/languageServerStarted").DoNotWait();
        }

        [JsonRpcMethod("shutdown")]
        public async Task Shutdown() {
            // Shutdown, but do not exit.
            // https://microsoft.github.io/language-server-protocol/specification#shutdown
            await _server.Shutdown();
            _shutdown = true;
            _idleTimeTracker.Dispose();
        }

        [JsonRpcMethod("exit")]
        public async Task Exit() {
            await _server.Exit();
            _sessionTokenSource.Cancel();
            _idleTimeTracker.Dispose();
            // Per https://microsoft.github.io/language-server-protocol/specification#exit
            Environment.Exit(_shutdown ? 0 : 1);
        }

        private Task LoadDirectoryFiles() {
            string rootDir = null;
            if (_initParams.rootUri != null) {
                rootDir = _initParams.rootUri.ToAbsolutePath();
            } else if (!string.IsNullOrEmpty(_initParams.rootPath)) {
                rootDir = PathUtils.NormalizePath(_initParams.rootPath);
            }

            if (string.IsNullOrEmpty(rootDir)) {
                return Task.CompletedTask;
            }

            var matcher = new Matcher();
            var included = _initParams.initializationOptions.includeFiles;
            matcher.AddIncludePatterns(included != null && included.Length > 0 ? included : new[] { "**/*" });
            matcher.AddExcludePatterns(_initParams.initializationOptions.excludeFiles ?? Enumerable.Empty<string>());

            var dib = new DirectoryInfoWrapper(new DirectoryInfo(rootDir));
            var matchResult = matcher.Execute(dib);

            _server.LogMessage(MessageType.Log, $"Loading files from {rootDir}");
            return LoadFromDirectoryAsync(rootDir, matchResult);
        }

        private async Task LoadFromDirectoryAsync(string rootDir, PatternMatchingResult matchResult) {
            foreach (var file in matchResult.Files) {
                if (_sessionTokenSource.IsCancellationRequested) {
                    break;
                }

                var path = Path.Combine(rootDir, PathUtils.NormalizePath(file.Path));
                if (!ModulePath.IsPythonSourceFile(path)) {
                    if (ModulePath.IsPythonFile(path, true, true, true)) {
                        // TODO: Deal with scrapable files (if we need to do anything?)
                    }
                    continue;
                }
                await _server.LoadFileAsync(new Uri(path));
            }
        }

        private void MonitorParentProcess(InitializeParams p) {
            // Monitor parent process
            Process parentProcess = null;
            if (p.processId.HasValue) {
                try {
                    parentProcess = Process.GetProcessById(p.processId.Value);
                } catch (ArgumentException) { }

                Debug.Assert(parentProcess != null, "Parent process does not exist");
                if (parentProcess != null) {
                    parentProcess.Exited += (s, e) => _sessionTokenSource.Cancel();
                }
            }

            if (parentProcess != null) {
                Task.Run(async () => {
                    while (!_sessionTokenSource.IsCancellationRequested) {
                        await Task.Delay(2000);
                        if (parentProcess.HasExited) {
                            _sessionTokenSource.Cancel();
                        }
                    }
                }).DoNotWait();
            }
        }

        private async Task IfTestWaitForAnalysisCompleteAsync() {
            if (_initParams.initializationOptions.testEnvironment) {
                await _server.WaitForCompleteAnalysisAsync();
            }
        }
    }
}
