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
using System.Threading.Tasks;
using Microsoft.PythonTools.Intellisense;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;

namespace Microsoft.PythonTools.Refactoring {
    using AP = AnalysisProtocol;
    class MethodExtractor {
        private readonly ITextView _view;
        private readonly IServiceProvider _serviceProvider;

        public MethodExtractor(IServiceProvider serviceProvider, ITextView textView) {
            _view = textView;
            _serviceProvider = serviceProvider;
        }

        public async Task<bool> ExtractMethod(IExtractMethodInput input) {
            var analyzer = _view.GetAnalyzer(_serviceProvider);
            var projectFile = _view.TextBuffer.GetAnalysisEntry();
            
            // extract once to validate the selection
            var extractInfo = await analyzer.ExtractMethodAsync(
                projectFile,
                _view.TextBuffer,
                _view,
                "method_name",
                null,
                null
            );
            if (extractInfo == null) {
                return false;
            }

            var extract = extractInfo.Data;
            if (extract.cannotExtractMsg != null) {
                input.CannotExtract(extract.cannotExtractMsg);
                return false;
            }

            if (extract.wasExpanded && !input.ShouldExpandSelection()) {
                return false;
            }

            if (extract.startIndex != null && extract.endIndex != null) {
                _view.Selection.Select(
                    new SnapshotSpan(
                        _view.TextBuffer.CurrentSnapshot,
                        Span.FromBounds(extract.startIndex.Value, extract.endIndex.Value)
                    ),
                    false
                );
            }

            var info = input.GetExtractionInfo(new ExtractedMethodCreator(analyzer, projectFile, _view, extract));
            if (info == null) {
                // user cancelled extract method
                return false;
            }

            // extract again to get the final result...
            extractInfo = await analyzer.ExtractMethodAsync(
                projectFile,
                _view.TextBuffer,
                _view,
                info.Name,
                info.Parameters,
                info.TargetScope?.Scope.id
            );

            if (extractInfo == null) {
                return false;
            }

            VsProjectAnalyzer.ApplyChanges(
                extractInfo.Data.changes,
                _view.TextBuffer,
                extractInfo.GetTracker(extractInfo.Data.version)
            );

            return true;
        }
    }

    class ExtractedMethodCreator {
        private readonly VsProjectAnalyzer _analyzer;
        private readonly AnalysisEntry _analysisEntry;
        private readonly ITextView _view;
        public AP.ExtractMethodResponse LastExtraction;

        public ExtractedMethodCreator(VsProjectAnalyzer analyzer, AnalysisEntry file, ITextView view, AP.ExtractMethodResponse initialExtraction) {
            _analyzer = analyzer;
            _analysisEntry = file;
            _view = view;
            LastExtraction = initialExtraction;
        }
        

        internal async Task<AP.ExtractMethodResponse> GetExtractionResult(ExtractMethodRequest info) {
            return LastExtraction = (await _analyzer.ExtractMethodAsync(
                _analysisEntry,
                _view.TextBuffer,
                _view,
                info.Name,
                info.Parameters,
                info.TargetScope?.Scope.id
            ).ConfigureAwait(false))?.Data;
        }
    }
}
