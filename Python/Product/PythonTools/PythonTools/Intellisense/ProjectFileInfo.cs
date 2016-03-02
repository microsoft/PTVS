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
    public sealed class ProjectFileInfo {
        public readonly int _fileId;
        public readonly string _path;
        public readonly VsProjectAnalyzer ProjectState;
        public IIntellisenseCookie AnalysisCookie;

        private readonly Dictionary<object, object> _properties = new Dictionary<object, object>();
        internal BufferParser BufferParser;

        public ProjectFileInfo(VsProjectAnalyzer analyzer, string path, int fileId) {
            ProjectState = analyzer;
            _path = path;
            _fileId = fileId;
        }

        public event EventHandler AnalysisComplete;

        internal void OnAnalysisComplete() {
            IsAnalyzed = true;
            AnalysisComplete?.Invoke(this, EventArgs.Empty);
        }

        internal event EventHandler ParseComplete;

        internal void OnParseComplete() {
            ParseComplete?.Invoke(this, EventArgs.Empty);
        }

        public string FilePath => _path;

        public int FileId => _fileId;

        public bool IsAnalyzed { get; internal set; }

        public Dictionary<object, object> Properties => _properties;

        public IEnumerable<MemberResult> GetAllAvailableMembers(SourceLocation location, GetMemberOptions options) {
            return ProjectState.GetAllAvailableMembers(this, location, options);
        }

        public IEnumerable<MemberResult> GetMembers(string text, SourceLocation location, GetMemberOptions options) {
            return ProjectState.GetMembers(this, text, location, options);
        }

        public IEnumerable<MemberResult> GetModuleMembers(string[] package, bool v) {
            return ProjectState.GetModuleMembers(this, package, v);
        }

        public IEnumerable<MemberResult> GetModules(bool v) {
            return ProjectState.GetModules(this, v);
        }

        internal IEnumerable<IAnalysisVariable> GetVariables(string expr, SourceLocation translatedLocation) {
            return ProjectState.GetVariables(this, expr, translatedLocation);
        }

        internal IEnumerable<AnalysisValue> GetValues(string expr, SourceLocation translatedLocation) {
            return ProjectState.GetValues(this, expr, translatedLocation);
        }

        internal string GetLine(int line) {
            return AnalysisCookie.GetLine(line);
        }
    }
}
