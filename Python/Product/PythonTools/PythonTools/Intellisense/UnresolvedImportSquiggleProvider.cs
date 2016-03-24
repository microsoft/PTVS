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
using System.Linq;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    sealed class UnresolvedImportSquiggleProvider {
        // Allows test cases to skip checking user options
        internal static bool _alwaysCreateSquiggle;
        private readonly IServiceProvider _serviceProvider;
        private readonly TaskProvider _taskProvider;

        public UnresolvedImportSquiggleProvider(IServiceProvider serviceProvider, TaskProvider taskProvider) {
            _serviceProvider = serviceProvider;
            _taskProvider = taskProvider;
        }

        public void ListenForNextNewAnalysis(AnalysisEntry entry, ITextBuffer buffer) {
            if (entry != null && !string.IsNullOrEmpty(entry.Path)) {
                buffer.RegisterForNewAnalysis(newEntry => OnNewAnalysis(newEntry, buffer));
                if (entry.IsAnalyzed) {
                    OnNewAnalysis(entry, buffer);
                }
            }
        }

        private async void OnNewAnalysis(AnalysisEntry entry, ITextBuffer buffer) {
            if (!_alwaysCreateSquiggle) {
                var service = _serviceProvider.GetPythonToolsService();
                if (service == null || !service.GeneralOptions.UnresolvedImportWarning) {
                    return;
                }
            }

            var missingImports = await entry.Analyzer.GetMissingImportsAsync(entry, buffer);
            if (missingImports != null) {
                var missing = missingImports.Data;

                if (missing.unresolved.Any()) {
                    var translator = missingImports.GetTracker(missingImports.Data.version);
                    if (translator != null) {
                        var f = new TaskProviderItemFactory(translator);

                        _taskProvider.ReplaceItems(
                            entry,
                            VsProjectAnalyzer.UnresolvedImportMoniker,
                            missingImports.Data.unresolved.Select(t => f.FromUnresolvedImport(
                                _serviceProvider,
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
}
