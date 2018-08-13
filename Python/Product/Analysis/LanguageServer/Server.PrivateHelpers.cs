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

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    public sealed partial class Server {
        private RestTextConverter _restTextConverter = new RestTextConverter();

        private MarkupContent GetMarkupContent(string plainTextContent, IEnumerable<string> preferences) {
            if(string.IsNullOrEmpty(plainTextContent)) {
                return string.Empty;
            }
            switch (SelectBestMarkup(preferences, MarkupKind.Markdown, MarkupKind.PlainText)) {
                case MarkupKind.Markdown:
                    return new MarkupContent {
                        kind = MarkupKind.Markdown,
                        value = _restTextConverter.ToMarkdown(plainTextContent)
                    };
            }
            return plainTextContent;
        }

        private string SelectBestMarkup(IEnumerable<string> requested, params string[] supported) {
            if (requested == null) {
                return supported.First();
            }
            foreach (var k in requested) {
                if (supported.Contains(k)) {
                    return k;
                }
            }
            return MarkupKind.PlainText;
        }
    }
}
