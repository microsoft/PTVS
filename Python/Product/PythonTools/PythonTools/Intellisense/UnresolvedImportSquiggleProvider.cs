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
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Options;
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    sealed class UnresolvedImportSquiggleProvider : IPythonTextBufferInfoEventSink {
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
            buffer.AddSink(typeof(UnresolvedImportSquiggleProvider), this);
            if (buffer.AnalysisEntry?.IsAnalyzed == true) {
                OnNewAnalysis(buffer, buffer.AnalysisEntry)
                    .HandleAllExceptions(_services.Site, GetType())
                    .DoNotWait();
            }
        }

        public void RemoveBuffer(PythonTextBufferInfo buffer) {
            buffer.RemoveSink(typeof(UnresolvedImportSquiggleProvider));
        }

        async Task IPythonTextBufferInfoEventSink.PythonTextBufferEventAsync(PythonTextBufferInfo sender, PythonTextBufferInfoEventArgs e) {
            if (e.Event == PythonTextBufferInfoEvents.NewAnalysis) {
                await OnNewAnalysis(sender, e.AnalysisEntry);
            }
        }

        private async Task OnNewAnalysis(PythonTextBufferInfo bi, AnalysisEntry entry) {
            if (!_enabled && !_alwaysCreateSquiggle || entry == null) {
                _taskProvider.Clear(bi.Filename, VsProjectAnalyzer.UnresolvedImportMoniker);
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
                        bi.Filename,
                        VsProjectAnalyzer.UnresolvedImportMoniker,
                        missingImports.Data.unresolved.Select(t => f.FromUnresolvedImport(
                            _services.Site,
                            entry.Analyzer.InterpreterFactory as IPythonInterpreterFactoryWithDatabase,
                            t.name,
                            new SourceSpan(
                                new SourceLocation(t.startLine, t.startColumn),
                                new SourceLocation(t.endLine, t.endColumn)
                            )
                        )).ToList()
                    );
                }
            } else {
                _taskProvider.Clear(bi.Filename, VsProjectAnalyzer.UnresolvedImportMoniker);
            }
        }
    }
}
