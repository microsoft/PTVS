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
using System.IO;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class MarkdownDocumentationBuilder : DocumentationBuilder {
        private readonly char[] _codeChars = new[] { '(', ')', '[', ']', '{', '}', '<', '>', '=' };

        public MarkdownDocumentationBuilder(InformationDisplayOptions displayOptions) : base(displayOptions) { }

        public override string GetModuleDocumentation(ModuleReference modRef) {
            var doc = modRef.Module?.Documentation;
            return doc != null 
                ? new RestTextConverter().ToMarkdown($"module {modRef.Name}{Environment.NewLine}{Environment.NewLine}{LimitLines(doc)}") 
                : $"module {modRef.Name}";
        }

        protected override string MakeFunctionDocumentation(AnalysisValue value) => FromDocAndDescription(value);
        protected override string MakeModuleDocumentation(AnalysisValue value) => FromDocAndDescription(value);
        protected override string MakeClassDocumentation(AnalysisValue value) => FromDocAndDescription(value);
        protected override string MakeConstantDocumentation(AnalysisValue value) => $"```python\n{value.Description}\n```";

        private string FromDocAndDescription(AnalysisValue value) {
            var sb = new StringBuilder();
            sb.AppendLine("```python");
            sb.AppendLine(value.Description);
            sb.AppendLine("```");
            var doc = LimitLines(value.Documentation).Trim();
            doc = new RestTextConverter().ToMarkdown(doc);
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(SoftWrap(doc));
            return sb.ToString();
        }

        protected override string SoftWrap(string s) {
            var result = new StringBuilder();
            using (var sr = new StringReader(s)) {
                for (var line = sr.ReadLine(); line != null;) {
                    var nextLine = sr.ReadLine();
                    if (!string.IsNullOrEmpty(line)
                        && !string.IsNullOrEmpty(nextLine)
                        && line.IndexOfAny(_codeChars) < 0
                        && nextLine.IndexOfAny(_codeChars) < 0
                        && !line.EndsWithOrdinal(".")
                        && Char.IsLower(nextLine[0])) {
                        result.Append(line);
                        result.Append(' ');
                    } else {
                        result.AppendLine(line);
                    }
                    line = nextLine;
                }
            }
            return result.ToString();
        }
    }
}
