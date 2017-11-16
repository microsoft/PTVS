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
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PythonTools.Editor;
using Microsoft.PythonTools.Editor.Core;
using Microsoft.PythonTools.Parsing;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;

namespace Microsoft.PythonTools.Intellisense {
    /// <summary>
    /// Provides squiggles and warning when file encoding does not match
    /// encoding specified in the Python '# -*- coding: ENCODING -*-'
    /// </summary>
    sealed class InvalidEncodingSquiggleProvider : BufferAnalysisSquiggleProviderBase<InvalidEncodingSquiggleProvider> {
        public InvalidEncodingSquiggleProvider(IServiceProvider serviceProvider, TaskProvider taskProvider) :
            base(serviceProvider,
                taskProvider,
                o => o.InvalidEncodingWarning,
                new[] { PythonTextBufferInfoEvents.NewAnalysis, PythonTextBufferInfoEvents.DocumentEncodingChanged }) {
        }

        protected override async Task OnNewAnalysis(PythonTextBufferInfo bi, AnalysisEntry entry) {
            if (!Enabled && !_alwaysCreateSquiggle) {
                TaskProvider.Clear(bi.Filename, VsProjectAnalyzer.InvalidEncodingMoniker);
                return;
            }

            var chunk = bi.CurrentSnapshot.GetText(new Span(0, Math.Min(bi.CurrentSnapshot.Length, 512)));
            Parser.GetEncodingFromMagicDesignator(chunk, out var encoding, out var magicEncodingName, out var magicEncodingIndex);

            var documentEncoding = bi.Document.Encoding;
            string message = null;

            if (encoding != null) {
                // Encoding is specified and is a valid name. 
                // Check if it matches encoding set on the document text buffer. 
                if (encoding.EncodingName != documentEncoding.EncodingName) {
                    message = string.Format(CultureInfo.InvariantCulture, Strings.WarningEncodingMismatch, documentEncoding.EncodingName);
                }
            } else if (encoding == null) {
                if (!string.IsNullOrEmpty(magicEncodingName)) {
                    // Encoding is specified but not recognized as a valid name
                    message = string.Format(CultureInfo.InvariantCulture, Strings.WarningInvalidEncoding, magicEncodingName);
                } else {
                    // Encoding is not specified. Python assumes UTF-8 so we need to verify it.
                    if (Encoding.UTF8.EncodingName != documentEncoding.EncodingName) {
                        message = string.Format(CultureInfo.InvariantCulture, Strings.WarningEncodingDifferentFromDefault, documentEncoding.EncodingName);
                    }
                }
            }

            if (message != null) {
                if (!bi.Buffer.Properties.TryGetProperty<string>(VsProjectAnalyzer.InvalidEncodingMoniker, out var prevMessage)
                    || prevMessage != message) {

                    bi.Buffer.Properties[VsProjectAnalyzer.InvalidEncodingMoniker] = message;
                    var version = bi.Buffer.CurrentSnapshot.Version;

                    TaskProvider.ReplaceItems(
                        bi.Filename,
                        VsProjectAnalyzer.InvalidEncodingMoniker,
                        new List<TaskProviderItem>() {
                            new TaskProviderItem(
                                Services.Site,
                                message,
                                new SnapshotSpan(bi.CurrentSnapshot, magicEncodingIndex, magicEncodingName.Length).ToSourceSpan(),
                                VSTASKPRIORITY.TP_NORMAL,
                                VSTASKCATEGORY.CAT_CODESENSE,
                                true,
                                new LocationTracker(version, bi.Buffer, version.VersionNumber)
                            )
                        });
                }
            } else {
                TaskProvider.Clear(bi.Filename, VsProjectAnalyzer.InvalidEncodingMoniker);
                bi.Buffer.Properties.RemoveProperty(VsProjectAnalyzer.InvalidEncodingMoniker);
            }
        }
    }
}
