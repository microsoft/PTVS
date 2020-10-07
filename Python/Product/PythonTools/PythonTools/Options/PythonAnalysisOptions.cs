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
using System.Linq;
using Microsoft.Python.Core;
using Microsoft.PythonTools.LanguageServerClient;

namespace Microsoft.PythonTools.Options {
    public sealed class PythonAnalysisOptions {
        private const string Category = "Analysis";
        private readonly PythonToolsService _service;

        public bool AutoSearchPaths { get; set; }
        public string DiagnosticMode { get; set; }
        public string LogLevel { get; set; }
        public string StubPath { get; set; }
        public string TypeCheckingMode { get; set; }
        public string[] TypeshedPaths { get; set; }
        public bool UseLibraryCodeForTypes { get; set; }
        public string[] ExtraPaths { get; set; }

        internal PythonAnalysisOptions(PythonToolsService service) {
            _service = service;
            Load();
        }

        public void Load() {
            var changed = false;
            var autoSearchPaths = _service.LoadBool(nameof(AutoSearchPaths), Category) ?? true;
            if(AutoSearchPaths != autoSearchPaths) {
                AutoSearchPaths = autoSearchPaths;
                changed = true;
            }

            var diagnosticMode = _service.LoadString(nameof(DiagnosticMode), Category) ?? PythonLanguageClient.DiagnosticMode.OpenFilesOnly;
            if(DiagnosticMode != diagnosticMode) {
                DiagnosticMode = diagnosticMode;
                changed = true;
            }

            var logLevel = _service.LoadString(nameof(LogLevel), Category) ?? PythonLanguageClient.LogLevel.Information;
            if (LogLevel != logLevel) {
                LogLevel = logLevel;
                changed = true;
            }

            var stubPath = _service.LoadString(nameof(StubPath), Category);
            if (StubPath != stubPath) {
                StubPath = stubPath;
                changed = true;
            }

            var typeCheckingMode = _service.LoadString(nameof(TypeCheckingMode), Category) ?? PythonLanguageClient.TypeCheckingMode.Basic;
            if (TypeCheckingMode != typeCheckingMode) {
                TypeCheckingMode = typeCheckingMode;
                changed = true;
            }

            var typeshedPaths = _service.LoadString(nameof(TypeshedPaths), Category)?.Split(';');
            if (!Enumerable.SequenceEqual(TypeshedPaths.MaybeEnumerate(), typeshedPaths.MaybeEnumerate())) {
                TypeshedPaths = typeshedPaths;
                changed = true;
            }

            var extraPaths = _service.LoadString(nameof(ExtraPaths), Category)?.Split(';');
            if (!Enumerable.SequenceEqual(ExtraPaths.MaybeEnumerate(), extraPaths.MaybeEnumerate())) {
                ExtraPaths = extraPaths;
                changed = true;
            }

            var useLibraryCodeForTypes = _service.LoadBool(nameof(UseLibraryCodeForTypes), Category) ?? true;
            if (UseLibraryCodeForTypes != useLibraryCodeForTypes) {
                UseLibraryCodeForTypes = useLibraryCodeForTypes;
                changed = true;
            }

            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Save() {
            var changed = _service.SaveBool(nameof(AutoSearchPaths), Category, AutoSearchPaths);
            changed |= _service.SaveString(nameof(DiagnosticMode), Category, DiagnosticMode);
            changed |= _service.SaveString(nameof(LogLevel), Category, LogLevel);
            changed |= _service.SaveString(nameof(StubPath), Category, StubPath);
            changed |= _service.SaveString(nameof(TypeCheckingMode), Category, TypeCheckingMode);
            changed |= _service.SaveString(nameof(TypeshedPaths), Category, TypeshedPaths != null ? string.Join(";", TypeshedPaths) : null);
            changed |= _service.SaveString(nameof(ExtraPaths), Category, ExtraPaths != null ? string.Join(";", ExtraPaths) : null);
            changed |= _service.SaveBool(nameof(UseLibraryCodeForTypes), Category, UseLibraryCodeForTypes);
            if (changed) {
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Reset() {
            AutoSearchPaths = true;
            DiagnosticMode = PythonLanguageClient.DiagnosticMode.OpenFilesOnly;
            LogLevel = PythonLanguageClient.LogLevel.Information;
            StubPath = null;
            TypeCheckingMode = PythonLanguageClient.TypeCheckingMode.Basic;
            TypeshedPaths = null;
            UseLibraryCodeForTypes = true;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public event EventHandler Changed;
    }
}
