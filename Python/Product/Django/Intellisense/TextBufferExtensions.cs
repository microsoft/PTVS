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

using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Projection;

namespace Microsoft.PythonTools.Django.Intellisense {
    internal static class TextBufferExtensions {
        public static string GetFileName(this ITextBuffer textBuffer) {
            string path = string.Empty;
            IEnumerable<ITextBuffer> searchBuffers = GetContributingBuffers(textBuffer);

            foreach (ITextBuffer buffer in searchBuffers) {
                ITextDocument document = null;
                if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out document)) {
                    path = document.FilePath ?? string.Empty;
                    if (!string.IsNullOrEmpty(path)) {
                        break;
                    }
                }
            }

            return path;
        }

        public static IEnumerable<ITextBuffer> GetContributingBuffers(this ITextBuffer textBuffer) {
            var allBuffers = new List<ITextBuffer>();

            allBuffers.Add(textBuffer);
            for (int i = 0; i < allBuffers.Count; i++) {
                IProjectionBuffer currentBuffer = allBuffers[i] as IProjectionBuffer;
                if (currentBuffer != null) {
                    foreach (ITextBuffer sourceBuffer in currentBuffer.SourceBuffers) {
                        if (!allBuffers.Contains(sourceBuffer))
                            allBuffers.Add(sourceBuffer);
                    }
                }
            }

            return allBuffers;
        }
    }
}
