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

using System.IO;

namespace Microsoft.PythonTools.Common.Core.IO {
    public sealed class FileSystemWatcherProxy : IFileSystemWatcher {
        private readonly FileSystemWatcher _fileSystemWatcher;

        public FileSystemWatcherProxy(string path, string filter) {
            _fileSystemWatcher = new FileSystemWatcher(path, filter);
        }

        public void Dispose() => _fileSystemWatcher.Dispose();

        public bool EnableRaisingEvents {
            get => _fileSystemWatcher.EnableRaisingEvents;
            set => _fileSystemWatcher.EnableRaisingEvents = value;
        }

        public bool IncludeSubdirectories {
            get => _fileSystemWatcher.IncludeSubdirectories;
            set => _fileSystemWatcher.IncludeSubdirectories = value;
        }

        public int InternalBufferSize {
            get => _fileSystemWatcher.InternalBufferSize;
            set => _fileSystemWatcher.InternalBufferSize = value;
        }

        public NotifyFilters NotifyFilter {
            get => _fileSystemWatcher.NotifyFilter;
            set => _fileSystemWatcher.NotifyFilter = value;
        }

        public event FileSystemEventHandler Changed {
            add => _fileSystemWatcher.Changed += value;
            remove => _fileSystemWatcher.Changed -= value;
        }

        public event FileSystemEventHandler Created {
            add => _fileSystemWatcher.Created += value;
            remove => _fileSystemWatcher.Created -= value;
        }

        public event FileSystemEventHandler Deleted {
            add => _fileSystemWatcher.Deleted += value;
            remove => _fileSystemWatcher.Deleted -= value;
        }

        public event RenamedEventHandler Renamed {
            add => _fileSystemWatcher.Renamed += value;
            remove => _fileSystemWatcher.Renamed -= value;
        }

        public event ErrorEventHandler Error {
            add => _fileSystemWatcher.Error += value;
            remove => _fileSystemWatcher.Error -= value;
        }
    }
}
