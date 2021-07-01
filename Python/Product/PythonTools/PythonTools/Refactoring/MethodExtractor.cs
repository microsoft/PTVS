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

using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Intellisense;

namespace Microsoft.PythonTools.Refactoring
{
    using AP = AnalysisProtocol;

    class MethodExtractor
    {
        private readonly ITextView _view;
        private readonly PythonEditorServices _services;

        public MethodExtractor(PythonEditorServices services, ITextView textView)
        {
            _view = textView;
            _services = services;
        }

        public static bool? CanExtract(ITextView view)
        {
            if (view.GetPythonBufferAtCaret() != null)
            {
                if (view.Selection.IsEmpty ||
                    view.Selection.Mode == TextSelectionMode.Box ||
                    String.IsNullOrWhiteSpace(view.Selection.StreamSelectionSpan.GetText()))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> ExtractMethod(IExtractMethodInput input)
        {
            var buffer = _view.GetPythonBufferAtCaret();
            var bi = _services.GetBufferInfo(buffer);
            var entry = bi?.AnalysisEntry;
            if (entry?.Analyzer == null)
            {
                return false;
            }

            var snapshot = buffer.CurrentSnapshot;

            // extract once to validate the selection
            var extract = await entry.Analyzer.ExtractMethodAsync(
                bi,
                _view,
                "method_name",
                null,
                null
            );
            if (extract == null)
            {
                return false;
            }

            if (extract.cannotExtractReason != AP.CannotExtractReason.None)
            {
                input.CannotExtract(GetCannotExtractMessage(extract.cannotExtractReason));
                return false;
            }

            if (extract.wasExpanded && !input.ShouldExpandSelection())
            {
                return false;
            }

            if (extract.startLine > 0 && extract.endLine > 0)
            {
                var selectionSpan = _view.BufferGraph.MapUpToBuffer(
                    new SourceSpan(
                        new SourceLocation(extract.startLine, extract.startCol),
                        new SourceLocation(extract.endLine, extract.endCol)
                    ).ToSnapshotSpan(snapshot),
                    SpanTrackingMode.EdgeInclusive,
                    _view.TextBuffer
                );

                foreach (var span in selectionSpan)
                {
                    _view.Selection.Select(span, false);
                    break;
                }
            }

            var info = input.GetExtractionInfo(new ExtractedMethodCreator(bi, _view, extract));
            if (info == null)
            {
                // user cancelled extract method
                return false;
            }

            // extract again to get the final result...
            extract = await entry.Analyzer.ExtractMethodAsync(
                bi,
                _view,
                info.Name,
                info.Parameters,
                info.TargetScope?.Scope.id
            );

            if (extract == null)
            {
                return false;
            }

            VsProjectAnalyzer.ApplyChanges(
                extract.changes,
                buffer,
                bi.LocationTracker,
                extract.version
            );

            return true;
        }

        private static string GetCannotExtractMessage(AP.CannotExtractReason reason)
        {
            switch (reason)
            {
                case AP.CannotExtractReason.InvalidTargetSelected:
                    return Strings.ExtractMethodInvalidTargetSelected;
                case AP.CannotExtractReason.InvalidExpressionSelected:
                    return Strings.ExtractMethodInvalidExpressionSelected;
                case AP.CannotExtractReason.MethodAssignsVariablesAndReturns:
                    return Strings.ExtractMethodAssignsVariablesAndReturns;
                case AP.CannotExtractReason.StatementsFromClassDefinition:
                    return Strings.ExtractMethodStatementsFromClassDefinition;
                case AP.CannotExtractReason.SelectionContainsBreakButNotEnclosingLoop:
                    return Strings.ExtractMethodSelectionContainsBreakButNotEnclosingLoop;
                case AP.CannotExtractReason.SelectionContainsContinueButNotEnclosingLoop:
                    return Strings.ExtractMethodSelectionContainsContinueButNotEnclosingLoop;
                case AP.CannotExtractReason.ContainsYieldExpression:
                    return Strings.ExtractMethodContainsYieldExpression;
                case AP.CannotExtractReason.ContainsFromImportStar:
                    return Strings.ExtractMethodContainsFromImportStar;
                case AP.CannotExtractReason.SelectionContainsReturn:
                    return Strings.ExtractMethodSelectionContainsReturn;
                default:
                    return null;
            }
        }
    }

    class ExtractedMethodCreator
    {
        private readonly PythonTextBufferInfo _buffer;
        private readonly ITextView _view;
        public AP.ExtractMethodResponse LastExtraction;
        internal PythonLanguageVersion PythonVersion => _buffer.LanguageVersion;

        public ExtractedMethodCreator(PythonTextBufferInfo buffer, ITextView view, AP.ExtractMethodResponse initialExtraction)
        {
            _buffer = buffer;
            _view = view;
            LastExtraction = initialExtraction;
        }


        internal async Task<AP.ExtractMethodResponse> GetExtractionResult(ExtractMethodRequest info)
        {
            return LastExtraction = (await _buffer.AnalysisEntry.Analyzer.ExtractMethodAsync(
                _buffer,
                _view,
                info.Name,
                info.Parameters,
                info.TargetScope?.Scope.id
            ).ConfigureAwait(false));
        }
    }
}
