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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.PythonTools.Common.Infrastructure;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.LanguageServer.Protocol;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.VSIntegration.Contracts;
using StreamJsonRpc;

namespace Microsoft.PythonTools.LanguageServerClient.FileWatcher {
    class Listener : IDisposable {
        private JsonRpc _rpc;
        
        private System.IO.FileSystemWatcher _solutionWatcher;
        private Microsoft.Extensions.FileSystemGlobbing.Matcher _matcher = new Microsoft.Extensions.FileSystemGlobbing.Matcher(StringComparison.OrdinalIgnoreCase);
        private bool disposedValue;
        private string _root;

        public Listener(StreamJsonRpc.JsonRpc rpc, IVsFolderWorkspaceService workspaceService, IServiceProvider site) {
            this._rpc = rpc;
            this._rpc.Disconnected += _rpc_Disconnected;

            // Ignore some common directories
            _matcher.AddExclude("**/.vs/**/*.*");

            // Ignore files that end with ~ or TMP
            _matcher.AddExclude("**/*~");
            _matcher.AddExclude("**/~*");
            _matcher.AddExclude("**/*TMP");
            _matcher.AddExclude("**/__pycache__");

            // Depending upon if this is a workspace or a solution, listen to different change events.
            if (workspaceService != null && workspaceService.CurrentWorkspace != null) {
                workspaceService.CurrentWorkspace.GetService<IFileWatcherService>().OnFileSystemChanged += OnFileChanged;
                _root = workspaceService.CurrentWorkspace.Location;
            } else {
                var path = GetSolutionDirectory(site);
                if (path != null) {
                    _solutionWatcher = new System.IO.FileSystemWatcher();
                    _solutionWatcher.Path = path;
                    _solutionWatcher.IncludeSubdirectories = true;
                    _solutionWatcher.NotifyFilter = 
                        NotifyFilters.LastWrite | NotifyFilters.CreationTime | 
                        NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.Size;
                    _solutionWatcher.Changed += OnFileChanged_Sync;
                    _solutionWatcher.Created += OnFileChanged_Sync;
                    _solutionWatcher.Deleted += OnFileChanged_Sync;
                    _solutionWatcher.Renamed += OnFileChanged_Sync;
                    _solutionWatcher.EnableRaisingEvents = true;
                    _root = _solutionWatcher.Path;
                }
            }
        }

        private void _rpc_Disconnected(object sender, JsonRpcDisconnectedEventArgs e) {
            _rpc.Disconnected -= _rpc_Disconnected;
            _rpc = null;
        }

        private string GetSolutionDirectory(IServiceProvider site) {
            try {
                var dte = (EnvDTE80.DTE2)site.GetService(typeof(EnvDTE.DTE));
                var path = dte?.Solution?.FileName;
                return Path.GetDirectoryName(path);

            } catch {
                return null;
            }

        }

        public void AddPatterns(FileSystemWatcher[] patterns) {
            Array.ForEach(patterns, p => AddPattern(p));
        }

        private void AddPattern(FileSystemWatcher pattern) {
            // Add to our matcher
            _matcher.AddInclude(pattern.GlobPattern);

            // Ignoring kind for now. Just tell about everything.
        }

        private bool Equals(System.IO.FileSystemWatcher l, System.IO.FileSystemWatcher r) {
            if (l == r) {
                return true;
            }
            if (l.Path == r.Path && l.IncludeSubdirectories == r.IncludeSubdirectories && r.Filter == l.Filter) {
                return true;
            }
            return false;
        }

        private async void OnFileChanged_Sync(object sender, System.IO.FileSystemEventArgs e) {
            await OnFileChanged(sender, e);
        }
        private async Task OnFileChanged(object sender, System.IO.FileSystemEventArgs e) {

            // Skip directory change events
            if (e.IsDirectoryChanged()) {
                return;
            }

            // Create something to match with
            var item = new InMemoryDirectoryInfo(_root, new string[] { e.FullPath });

            // See if this matches one of our patterns.
            if (_matcher.Execute(item).HasMatches) {
                // Send out the event to the language server
                var renamedArgs = e as System.IO.RenamedEventArgs;
                var didChangeParams = new DidChangeWatchedFilesParams();

                // Visual Studio actually does a rename when saving. The rename is from a file ending with '~'
                if (renamedArgs == null || renamedArgs.OldFullPath.EndsWith("~")) {
                    renamedArgs = null;
                    didChangeParams.Changes = new FileEvent[] { new FileEvent() };
                    didChangeParams.Changes[0].Uri = new Uri(e.FullPath);

                    switch (e.ChangeType) {
                        case WatcherChangeTypes.Created:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Created;
                            break;
                        case WatcherChangeTypes.Deleted:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Deleted;
                            break;
                        case WatcherChangeTypes.Changed:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Changed;
                            break;
                        case WatcherChangeTypes.Renamed:
                            didChangeParams.Changes[0].FileChangeType = FileChangeType.Changed;
                            break;

                        default:
                            didChangeParams.Changes = Array.Empty<FileEvent>();
                            break;
                    }
                } else {
                    // file renamed
                    var deleteEvent = new FileEvent();
                    deleteEvent.FileChangeType = FileChangeType.Deleted;
                    deleteEvent.Uri = new Uri(renamedArgs.OldFullPath);

                    var createEvent = new FileEvent();
                    createEvent.FileChangeType = FileChangeType.Created;
                    createEvent.Uri = new Uri(renamedArgs.FullPath);

                    didChangeParams.Changes = new FileEvent[] { deleteEvent, createEvent };
                }

                if (didChangeParams.Changes.Any() && this._rpc != null) {
             
                    await _rpc.NotifyWithParameterObjectAsync(Methods.WorkspaceDidChangeWatchedFiles.Name, didChangeParams);
                }
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    _solutionWatcher?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Listener()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
