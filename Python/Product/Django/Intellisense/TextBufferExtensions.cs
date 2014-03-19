/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
