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

namespace Microsoft.PythonTools.Intellisense
{
    /// <summary>
    /// Provides squiggles and warning when file encoding does not match
    /// encoding specified in the Python '# -*- coding: ENCODING -*-'
    /// </summary>
    sealed class InvalidEncodingSquiggleProvider : BufferAnalysisSquiggleProviderBase<InvalidEncodingSquiggleProvider>
    {
        public InvalidEncodingSquiggleProvider(IServiceProvider serviceProvider, TaskProvider taskProvider) :
            base(serviceProvider,
                taskProvider,
                o => o.InvalidEncodingWarning,
                new[] { PythonTextBufferInfoEvents.TextContentChangedLowPriority, PythonTextBufferInfoEvents.NewAnalysis, PythonTextBufferInfoEvents.DocumentEncodingChanged })
        {
        }

        protected override async Task OnNewAnalysis(PythonTextBufferInfo bi, AnalysisEntry entry)
        {
            if (!Enabled && !_alwaysCreateSquiggle || bi?.Document == null || bi.Buffer?.Properties == null)
            {
                TaskProvider.Clear(bi.Filename, VsProjectAnalyzer.InvalidEncodingMoniker);
                return;
            }

            var snapshot = bi.CurrentSnapshot;

            var message = CheckEncoding(snapshot, bi.Document.Encoding, out var magicEncodingName, out var magicEncodingIndex);
            if (message != null)
            {
                if (!bi.Buffer.Properties.TryGetProperty<string>(VsProjectAnalyzer.InvalidEncodingMoniker, out var prevMessage)
                    || prevMessage != message)
                {

                    bi.Buffer.Properties[VsProjectAnalyzer.InvalidEncodingMoniker] = message;
                    SourceSpan span;
                    if (string.IsNullOrEmpty(magicEncodingName))
                    {
                        var pt = new SnapshotPoint(snapshot, magicEncodingIndex).ToSourceLocation();
                        span = new SourceSpan(pt, new SourceLocation(pt.Line, int.MaxValue));
                    }
                    else
                    {
                        span = new SnapshotSpan(snapshot, magicEncodingIndex, magicEncodingName.Length).ToSourceSpan();
                    }

                    TaskProvider.ReplaceItems(
                        bi.Filename,
                        VsProjectAnalyzer.InvalidEncodingMoniker,
                        new List<TaskProviderItem> {
                            new TaskProviderItem(
                                Services.Site,
                                VsProjectAnalyzer.InvalidEncodingMoniker,
                                message,
                                span,
                                VSTASKPRIORITY.TP_NORMAL,
                                VSTASKCATEGORY.CAT_CODESENSE,
                                true,
                                bi.LocationTracker,
                                snapshot.Version.VersionNumber
                            )
                        });
                }
            }
            else
            {
                TaskProvider.Clear(bi.Filename, VsProjectAnalyzer.InvalidEncodingMoniker);
                bi.Buffer.Properties.RemoveProperty(VsProjectAnalyzer.InvalidEncodingMoniker);
            }
        }

        internal static string CheckEncoding(ITextSnapshot snapshot, Encoding documentEncoding, out string magicEncodingName, out int magicEncodingIndex)
        {
            var chunk = snapshot.GetText(new Span(0, Math.Min(snapshot.Length, 512)));
            Parser.GetEncodingFromMagicDesignator(chunk, out var encoding, out magicEncodingName, out magicEncodingIndex);

            string message = null;
            if (encoding != null)
            {
                // Encoding is specified and is a valid name. 
                // Check if it matches encoding set on the document text buffer. 
                if (encoding.EncodingName != documentEncoding.EncodingName)
                {
                    message = Strings.WarningEncodingMismatch.FormatUI(documentEncoding.EncodingName, magicEncodingName);
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(magicEncodingName))
                {
                    // Encoding is specified but not recognized as a valid name
                    message = Strings.WarningInvalidEncoding.FormatUI(magicEncodingName);
                }
                else
                {
                    // Encoding is not specified. Python assumes UTF-8 so we need to verify it.
                    if (Encoding.UTF8.EncodingName != documentEncoding.EncodingName)
                    {
                        message = Strings.WarningEncodingDifferentFromDefault.FormatUI(documentEncoding.EncodingName);
                    }
                }
            }
            return message;
        }
    }
}
