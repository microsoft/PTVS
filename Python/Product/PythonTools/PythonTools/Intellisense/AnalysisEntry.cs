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
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Represents a file which is being analyzed.  Tracks the file ID in the out of proc analysis,
    /// the path to the file, the analyzer, and the buffer parser being used to track changes to edits
    /// amongst o
    /// </summary>
    internal sealed class AnalysisEntry {
        private readonly int _fileId;
        private readonly string _path;
        public readonly VsProjectAnalyzer _analyzer;
        private readonly Dictionary<object, object> _properties = new Dictionary<object, object>();

        internal IIntellisenseCookie _cookie;
        internal BufferParser BufferParser;

        /// <summary>
        /// Raised when a new analysis is available for this AnalyisEntry
        /// </summary>
        public event EventHandler AnalysisComplete;
        
        private static readonly object _searchPathEntryKey = new { Name = "SearchPathEntry" };

        public AnalysisEntry(VsProjectAnalyzer analyzer, string path, int fileId) {
            _analyzer = analyzer;
            _path = path;
            _fileId = fileId;
        }

        internal void OnAnalysisComplete() {
            IsAnalyzed = true;
            AnalysisComplete?.Invoke(this, EventArgs.Empty);

            var bufferParser = BufferParser;
            if (bufferParser != null) {
                foreach (var buffer in bufferParser.Buffers) {
                    var events = buffer.GetNewAnalysisRegistrations();
                    foreach (var notify in events) {
                        notify(this);
                    }
                }
            }
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
            return AnalysisCookie.GetLine(line);
        }

        internal void OnParseComplete() {
            var bufferParser = BufferParser;
            if (bufferParser != null) {
                foreach (var buffer in bufferParser.Buffers) {
                    var events = buffer.GetParseTreeRegistrations();
                    foreach (var notify in events) {
                        notify(this);
                    }
                }
            }
        }

        internal void OnNewAnalysisEntry() {
            var bufferParser = BufferParser;
            if (bufferParser != null) {
                foreach (var buffer in bufferParser.Buffers) {
                    var events = buffer.GetNewAnalysisEntryRegistrations();
                    foreach (var notify in events) {
                        notify(this);
                    }
                }
            }
        }

        public string SearchPathEntry {
            get {
                object result;
                Properties.TryGetValue(_searchPathEntryKey, out result);
                return (string)result;
            }
            set {
                Properties[_searchPathEntryKey] = value;
            }
        }

        public int GetBufferId(ITextBuffer buffer) {
            var bufferParser = BufferParser;
            if (bufferParser != null) {
                return bufferParser.GetBufferId(buffer);
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
            var bufferParser = BufferParser;
            if (bufferParser != null) {
                return bufferParser.GetAnalysisVersion(buffer);
            }

            // See GetBufferId above, this is really just defense in depth...
            return buffer.CurrentSnapshot.Version;
        }

        public async Task EnsureCodeSyncedAsync(ITextBuffer buffer) {
            // See GetBufferId above, this is really just defense in depth...
            var bufferParser = BufferParser;
            if (bufferParser != null) {
                await bufferParser.EnsureCodeSynced(buffer);
            }
        }
    }
}
