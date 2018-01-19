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
using Microsoft.PythonTools.Parsing;

namespace Microsoft.PythonTools.Intellisense {
    sealed class UnresolvedImportSquiggleProvider : BufferAnalysisSquiggleProviderBase<UnresolvedImportSquiggleProvider> {

        public UnresolvedImportSquiggleProvider(IServiceProvider serviceProvider, TaskProvider taskProvider):
            base(serviceProvider, taskProvider, o => o.UnresolvedImportWarning, new[] { PythonTextBufferInfoEvents.NewAnalysis }) {
        }

        protected override async Task OnNewAnalysis(PythonTextBufferInfo bi, AnalysisEntry entry) {
            if (!Enabled && !_alwaysCreateSquiggle || entry == null) {
                TaskProvider.Clear(bi.Filename, VsProjectAnalyzer.UnresolvedImportMoniker);
                return;
            }

            var missingImports = await entry.Analyzer.GetMissingImportsAsync(bi);
            if (missingImports == null) {
                return;
            }

            if (missingImports.unresolved.MaybeEnumerate().Any()) {
                var f = new TaskProviderItemFactory(bi.LocationTracker, missingImports.version);

                TaskProvider.ReplaceItems(
                    bi.Filename,
                    VsProjectAnalyzer.UnresolvedImportMoniker,
                    missingImports.unresolved.Select(t => f.FromUnresolvedImport(
                        Services.Site,
                        entry.Analyzer.InterpreterFactory,
                        t.name,
                        new SourceSpan(
                            new SourceLocation(t.startLine, t.startColumn),
                            new SourceLocation(t.endLine, t.endColumn)
                        )
                    )).ToList()
                );
            } else {
                TaskProvider.Clear(bi.Filename, VsProjectAnalyzer.UnresolvedImportMoniker);
            }
        }
    }
}
