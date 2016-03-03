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
using System.Windows.Threading;
using Microsoft.PythonTools.Analysis;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;
using Microsoft.PythonTools.Project;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Adornments;
using Microsoft.VisualStudio.Text.Tagging;

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

        public void ListenForNextNewAnalysis(ProjectFileInfo entry) {
            if (entry != null && !string.IsNullOrEmpty(entry.Path)) {
                entry.AnalysisComplete += OnNewAnalysis;
            }
        }

        public void StopListening(ProjectFileInfo entry) {
            if (entry != null) {
                entry.AnalysisComplete -= OnNewAnalysis;
                _taskProvider.Clear(entry, VsProjectAnalyzer.UnresolvedImportMoniker);
            }
        }

        private async void OnNewAnalysis(object sender, EventArgs e) {
            if (!_alwaysCreateSquiggle) {
                var service = _serviceProvider.GetPythonToolsService();
                if (service == null || !service.GeneralOptions.UnresolvedImportWarning) {
                    return;
                }

                ProjectFileInfo entry = sender as ProjectFileInfo;
                var missingImports = await entry.Analyzer.GetMissingImports(entry);
                
                foreach (var buffer in missingImports) {
                    if (buffer.unresolved.Any()) {
                        var translator = new LocationTracker(
                            entry,
                            buffer.bufferId,
                            buffer.version
                        );

                        var f = new TaskProviderItemFactory(translator);

                        _taskProvider.ReplaceItems(
                            entry,
                            VsProjectAnalyzer.UnresolvedImportMoniker,
                            buffer.unresolved.Select(t => f.FromUnresolvedImport(
                                _serviceProvider,
                                entry.Analyzer.InterpreterFactory as IPythonInterpreterFactoryWithDatabase,
                                t.name,
                                new SourceSpan(
                                    new SourceLocation(t.startIndex, t.startLine, t.startColumn),
                                    new SourceLocation(t.endIndex, t.endLine, t.endColumn)
                                )
                            )).ToList()
                        );
                    } else {
                        _taskProvider.Clear(entry, VsProjectAnalyzer.UnresolvedImportMoniker);
                    }
                }
            }
        }

    }
}
