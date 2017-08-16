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
using System.Linq;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    sealed class UnresolvedImportSquiggleProvider {
        // Allows test cases to skip checking user options
        internal static bool _alwaysCreateSquiggle;
        private readonly PythonEditorServices _services;
        private readonly TaskProvider _taskProvider;
        private bool _enabled;

        public UnresolvedImportSquiggleProvider(IServiceProvider serviceProvider, TaskProvider taskProvider) {
            if (taskProvider == null) {
                throw new ArgumentNullException(nameof(taskProvider));
            }
            _services = serviceProvider.GetComponentModel().GetService<PythonEditorServices>();
            _taskProvider = taskProvider;
            var options = _services.Python?.GeneralOptions;
            if (options != null) {
                _enabled = options.UnresolvedImportWarning;
                options.Changed += GeneralOptions_Changed;
            }
        }

        private void GeneralOptions_Changed(object sender, EventArgs e) {
            var options = sender as GeneralOptions;
            if (options != null) {
                _enabled = options.UnresolvedImportWarning;
            }
        }

        public void AddBuffer(PythonTextBufferInfo buffer) {
            buffer.OnNewAnalysis += OnNewAnalysis;
            if (buffer.AnalysisEntry?.IsAnalyzed == true) {
                OnNewAnalysis(buffer, EventArgs.Empty);
            }
        }

        public void RemoveBuffer(PythonTextBufferInfo buffer) {
            buffer.OnNewAnalysis -= OnNewAnalysis;
        }

        private async void OnNewAnalysis(object sender, EventArgs e) {
            var bi = sender as PythonTextBufferInfo;
            if (bi == null) {
                Debug.Fail("Unexpected Sender type " + sender?.GetType() ?? "(null)");
                return;
            }
            if (!_enabled && !_alwaysCreateSquiggle) {
                _taskProvider.Clear(bi.AnalysisEntry, VsProjectAnalyzer.UnresolvedImportMoniker);
                return;
            }

            var entry = bi.AnalysisEntry;
            if (entry == null) {
                return;
            }

            var missingImports = await entry.Analyzer.GetMissingImportsAsync(entry, bi.Buffer);
            if (missingImports == null) {
                return;
            }

            if (missingImports.Data.unresolved.Any()) {
                var translator = missingImports.GetTracker(missingImports.Data.version);
                if (translator != null) {
                    var f = new TaskProviderItemFactory(translator);

                    _taskProvider.ReplaceItems(
                        entry,
                        VsProjectAnalyzer.UnresolvedImportMoniker,
                        missingImports.Data.unresolved.Select(t => f.FromUnresolvedImport(
                            _services.Site,
                            entry.Analyzer.InterpreterFactory as IPythonInterpreterFactoryWithDatabase,
                            t.name,
                            new SourceSpan(
                                new SourceLocation(t.startIndex, t.startLine, t.startColumn),
                                new SourceLocation(t.endIndex, t.endLine, t.endColumn)
                            )
                        )).ToList()
                    );
                }
            } else {
                _taskProvider.Clear(entry, VsProjectAnalyzer.UnresolvedImportMoniker);
            }
        }
    }
}
