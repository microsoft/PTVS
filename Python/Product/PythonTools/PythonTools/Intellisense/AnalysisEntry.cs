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
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Represents a file which is being analyzed.  Tracks the file ID in the out of proc analysis,
    /// the path to the file, the analyzer, and the buffer parser being used to track changes to edits
    /// amongst o
    /// </summary>
    internal sealed class AnalysisEntry : IDisposable {
        private readonly WeakReference<BufferParser> _bufferParser;

        public readonly bool IsTemporaryFile;
        public readonly bool SuppressErrorList;

        /// <summary>
        /// Raised when a new parse tree is available for this AnalysisEntry
        /// </summary>
        public event EventHandler ParseComplete;
        /// <summary>
        /// Raised when a new analysis is available for this AnalyisEntry
        /// </summary>
        public event EventHandler AnalysisComplete;

        public AnalysisEntry(
            VsProjectAnalyzer analyzer,
            string path,
            int fileId,
            bool isTemporaryFile = false,
            bool suppressErrorList = false
        ) {
            Analyzer = analyzer;
            Path = path;
            FileId = fileId;
            Properties = new Dictionary<object, object>();
            IsTemporaryFile = isTemporaryFile;
            SuppressErrorList = suppressErrorList;
            _bufferParser = new WeakReference<BufferParser>(null);
        }

        public void Dispose() {
            BufferParser parser;
            if (_bufferParser.TryGetTarget(out parser) && parser != null) {
                parser.Dispose();
            }
        }

        internal void OnAnalysisComplete() {
            IsAnalyzed = true;
            AnalysisComplete?.Invoke(this, EventArgs.Empty);
        }

        internal void OnParseComplete() {
            ParseComplete?.Invoke(this, EventArgs.Empty);
        }

        internal BufferParser TryGetBufferParser() {
            BufferParser parser;
            return _bufferParser.TryGetTarget(out parser) ? parser : null;
        }

        internal BufferParser GetOrCreateBufferParser(PythonEditorServices services) {
            BufferParser parser;
            if (!_bufferParser.TryGetTarget(out parser)) {
                parser = new BufferParser(services, Analyzer, Path) {
                    IsTemporaryFile = IsTemporaryFile,
                    SuppressErrorList = SuppressErrorList
                };
                _bufferParser.SetTarget(parser);
            }
            return parser;
        }

        public VsProjectAnalyzer Analyzer { get; }

        public IIntellisenseCookie AnalysisCookie { get; set; }

        public string Path { get; }
        public int FileId { get; }
        public bool IsAnalyzed { get; internal set; }

        public Dictionary<object, object> Properties { get; }

        public string GetLine(int line) {
            return AnalysisCookie?.GetLine(line);
        }

        public int GetBufferId(ITextBuffer buffer) {
            // May get null if there is no analysis entry associated with the file yet.
            // This can happen when you have a document that is open but hasn't had focus
            // causing the full load of our intellisense controller.  In that case there
            // is only a single buffer which is buffer 0.  An easy repro for this is to
            // open a IronPython WPF project and close it with the XAML file focused and
            // the .py file still open. Re-open the project, and double click on a button
            // on the XAML page.  The python file isn't loaded and we have no 
            // PythonTextBufferInfo associated with it.
            return PythonTextBufferInfo.TryGetForBuffer(buffer)?.AnalysisBufferId ?? 0;
        }

        public async Task EnsureCodeSyncedAsync(ITextBuffer buffer) {
            try {
                var bufferParser = TryGetBufferParser();
                if (bufferParser != null) {
                    await bufferParser.EnsureCodeSyncedAsync(buffer);
                }
            } catch (OperationCanceledException) {
            }
        }
    }
}
