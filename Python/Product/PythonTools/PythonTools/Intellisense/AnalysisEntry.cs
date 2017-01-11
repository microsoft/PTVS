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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Represents a file which is being analyzed.  Tracks the file ID in the out of proc analysis,
    /// the path to the file, the analyzer, and the buffer parser being used to track changes to edits
    /// amongst o
    /// </summary>
    internal sealed class AnalysisEntry : IDisposable {
        private readonly int _fileId;
        private readonly string _path;
        public readonly VsProjectAnalyzer _analyzer;
        private readonly Dictionary<object, object> _properties = new Dictionary<object, object>();

        internal IIntellisenseCookie _cookie;
        private readonly object _bufferParserLock = new object();
        private BufferParser _bufferParser;
        private TaskCompletionSource<BufferParser> _bufferParserTask, _createBufferParserTask;

        /// <summary>
        /// Raised when a new analysis is available for this AnalyisEntry
        /// </summary>
        public event EventHandler AnalysisComplete;
        public readonly bool IsTemporaryFile;
        public readonly bool SuppressErrorList;

        public AnalysisEntry(VsProjectAnalyzer analyzer, string path, int fileId, bool isTemporaryFile = false, bool suppressErrorList = false) {
            _analyzer = analyzer;
            _path = path;
            _fileId = fileId;
            IsTemporaryFile = isTemporaryFile;
            SuppressErrorList = suppressErrorList;
        }

        public void Dispose() {
            _bufferParser?.Dispose();
        }

        internal async void OnAnalysisComplete() {
            IsAnalyzed = true;
            AnalysisComplete?.Invoke(this, EventArgs.Empty);

            BufferParser bufferParser;
            try {
                bufferParser = await GetBufferParserAsync();
            } catch (OperationCanceledException) {
                return;
            }

            foreach (var notify in bufferParser.Buffers.SelectMany(b => b.GetNewAnalysisRegistrations())) {
                notify(this);
            }
        }

        private void NotifyAllRegistrations(BufferParser bufferParser) {
            foreach (var notify in bufferParser.Buffers.SelectMany(b => b.GetNewAnalysisEntryRegistrations())) {
                notify(this);
            }
            foreach (var notify in bufferParser.Buffers.SelectMany(b => b.GetParseTreeRegistrations())) {
                notify(this);
            }
            foreach (var notify in bufferParser.Buffers.SelectMany(b => b.GetNewAnalysisRegistrations())) {
                notify(this);
            }
        }

        internal BufferParser TryGetBufferParser() {
            lock (_bufferParserLock) {
                return _bufferParser;
            }
        }

        internal Task<BufferParser> GetBufferParserAsync() {
            lock (_bufferParserLock) {
                if (_bufferParser != null) {
                    return Task.FromResult(_bufferParser);
                }
                if (_bufferParserTask == null) {
                    _bufferParserTask = new TaskCompletionSource<BufferParser>();
                }
                return _bufferParserTask.Task;
            }
        }

        internal void ClearBufferParser(BufferParser expected) {
            lock (_bufferParserLock) {
                Debug.Assert(_bufferParser != null && expected == _bufferParser);
                _bufferParser = null;
                _bufferParserTask?.TrySetCanceled();
                _bufferParserTask = null;
                _createBufferParserTask?.TrySetCanceled();
                _createBufferParserTask = null;
            }
        }

        internal async Task<BufferParser> GetOrCreateBufferParser(
            VsProjectAnalyzer analyzer,
            ITextBuffer buffer,
            Action<BufferParser> onCreate,
            Action<BufferParser> onGet
        ) {
            BufferParser bp;
            TaskCompletionSource<BufferParser> bpt = null, cbpt = null, tcs = null;

            lock (_bufferParserLock) {
                bp = _bufferParser;
                if (bp == null) {
                    cbpt = _createBufferParserTask;
                    bpt = _bufferParserTask;
                    if (cbpt == null) {
                        _createBufferParserTask = tcs = new TaskCompletionSource<BufferParser>();
                    }
                }
            }

            // There is an existing task doing creation, so wait on it.
            if (cbpt != null) {
                bp = await cbpt.Task;
            }

            if (bp != null) {
                onGet(bp);
                return bp;
            }

            bp = await BufferParser.CreateAsync(this, analyzer, buffer);
            lock (_bufferParserLock) {
                _bufferParser = bp;
                _bufferParserTask = null;
                _createBufferParserTask = null;
            }
            onCreate(bp);
            tcs.TrySetResult(bp);
            bpt?.TrySetResult(bp);

            NotifyAllRegistrations(bp);

            return bp;
        }

        public VsProjectAnalyzer Analyzer => _analyzer;

        public IIntellisenseCookie AnalysisCookie {
            get { return _cookie; }
            set { _cookie = value; }
        }

        public string Path => _path;

        public int FileId => _fileId;

        public bool IsAnalyzed { get; internal set; }

        public Dictionary<object, object> Properties => _properties;

        public string GetLine(int line) {
            return AnalysisCookie?.GetLine(line);
        }

        internal async void OnParseComplete() {
            BufferParser bufferParser;
            try {
                bufferParser = await GetBufferParserAsync();
            } catch (OperationCanceledException) {
                return;
            }
            foreach (var buffer in bufferParser.Buffers) {
                var events = buffer.GetParseTreeRegistrations();
                foreach (var notify in events) {
                    notify(this);
                }
            }
        }

        internal async void OnNewAnalysisEntry() {
            BufferParser bufferParser;
            try {
                bufferParser = await GetBufferParserAsync();
            } catch (OperationCanceledException) {
                return;
            }
            foreach (var buffer in bufferParser.Buffers) {
                var events = buffer.GetNewAnalysisEntryRegistrations();
                foreach (var notify in events) {
                    notify(this);
                }
            }
        }

        public int GetBufferId(ITextBuffer buffer) {
            var bufferParser = _bufferParser;
            if (bufferParser != null) {
                var res = bufferParser.GetBufferId(buffer);
                if (res != null) {
                    return res.Value;
                }
                // Race with the buffer closing...
            }

            // No buffer parser associated with the file yet.  This can happen when
            // you have a document that is open but hasn't had focus causing the full
            // load of our intellisense controller.  In that case there is only a single
            // buffer which is buffer 0.  An easy repro for this is to open a IronPython WPF
            // project and close it with the XAML file focused and the .py file still open.
            // Re-open the project, and double click on a button on the XAML page.  The python
            // file isn't loaded and weh ave no BufferParser associated with it.
            return 0;
        }

        public ITextVersion GetAnalysisVersion(ITextBuffer buffer) {
            var bufferParser = _bufferParser;
            if (bufferParser != null) {
                var res = bufferParser.GetAnalysisVersion(buffer);
                if (res != null) {
                    // Analysis version has gone away, this can happen
                    // if the text view is getting closed while we're
                    // trying to perform an operation.
                    return res;
                }
            }

            // See GetBufferId above, this is really just defense in depth...
            return buffer.CurrentSnapshot.Version;
        }

        public async Task EnsureCodeSyncedAsync(ITextBuffer buffer) {
            try {
                var bufferParser = await GetBufferParserAsync();
                await bufferParser.EnsureCodeSyncedAsync(buffer);
            } catch (OperationCanceledException) {
            }
        }
    }
}
