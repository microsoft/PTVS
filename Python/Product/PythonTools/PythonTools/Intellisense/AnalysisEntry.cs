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
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Represents a file which is being analyzed.  Tracks the file ID in the out of proc analysis,
    /// the path to the file, the analyzer, and the buffer parser being used to track changes to edits
    /// amongst o
    /// </summary>
    public sealed class AnalysisEntry {
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
    }
}
