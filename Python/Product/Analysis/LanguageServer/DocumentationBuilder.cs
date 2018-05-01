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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal abstract class DocumentationBuilder {
        private const string _ellipsis = "...";

        public InformationDisplayOptions DisplayOptions { get; }

        public static DocumentationBuilder Create(InformationDisplayOptions displayOptions) {
            displayOptions = displayOptions ?? Server.DisplayOptions;

            if (displayOptions.preferredFormat == MarkupKind.Markdown) {
                return new MarkdownDocumentationBuilder(displayOptions);
            }
            return new PlainTextDocumentationBuilder(displayOptions);
        }

        public DocumentationBuilder(InformationDisplayOptions displayOptions) {
            DisplayOptions = displayOptions;
        }

        public string GetDocumentation(IEnumerable<AnalysisValue> values, string originalExpression) {
            if (values.Count() == 1) {
                var v = values.First();
                switch (v.MemberType) {
                    case PythonMemberType.Function:
                        return MakeFunctionDocumentation(values.First());
                    case PythonMemberType.Class:
                        return MakeClassDocumentation(values.First());
                    case PythonMemberType.Module:
                        return MakeModuleDocumentation(values.First());
                }
            }
            return MakeGeneralDocumentation(values, originalExpression);
        }

        private string MakeGeneralDocumentation(IEnumerable<AnalysisValue> values, string originalExpression) {
            var documentations = new HashSet<string>();
            var descriptions = new HashSet<string>();

            foreach (var v in values) {
                var d = v.Description;
                if (v.MemberType == PythonMemberType.Instance || v.MemberType == PythonMemberType.Constant) {
                    if (!string.IsNullOrEmpty(d)) {
                        descriptions.Add(d);
                    }
                    continue;
                }

                var doc = v.Documentation;
                if (DisplayOptions.trimDocumentationLines) {
                    doc = LimitLines(doc);
                }

                if ((d?.Length ?? 0) < (doc?.Length ?? 0)) {
                    if (!string.IsNullOrEmpty(doc)) {
                        documentations.Add(doc);
                    }
                }

                if (!string.IsNullOrEmpty(d)) {
                    descriptions.Add(d);
                }
            }

            if (!descriptions.Any()) {
                return string.Empty;
            }

            var result = new StringBuilder();
            var count = 0;
            foreach (var d in descriptions.Ordered()) {
                if (count > 0) {
                    result.Append(", ");
                }
                result.Append(d);
                count++;
            }

            var multiline = result.ToString().IndexOf('\n') >= 0;
            var expressionPosition = 0;
            if (DisplayOptions.preferredFormat == MarkupKind.Markdown) {
                var languagePrefix = $"```python{Environment.NewLine}";
                result.Insert(0, languagePrefix);
                expressionPosition = languagePrefix.Length;
            }
            result.AppendLine();
            if (DisplayOptions.preferredFormat == MarkupKind.Markdown) {
                result.AppendLine("```");
            }
            result.AppendLine();

            foreach (var d in documentations.Ordered()) {
                result.AppendLine();
                result.AppendLine(SoftWrap(d));
            }

            if (DisplayOptions.trimDocumentationText && result.Length > DisplayOptions.maxDocumentationTextLength) {
                result.Length = Math.Max(0, DisplayOptions.maxDocumentationTextLength - 3);
                result.Append(_ellipsis);
            } else if (DisplayOptions.trimDocumentationLines) {
                using (var sr = new StringReader(result.ToString())) {
                    result.Clear();
                    int lines = DisplayOptions.maxDocumentationLineLength;
                    for (var line = sr.ReadLine(); line != null; line = sr.ReadLine()) {
                        if (--lines < 0) {
                            result.Append(_ellipsis);
                            break;
                        }
                        result.AppendLine(line);
                    }
                }
            }

            while (result.Length > 0 && char.IsWhiteSpace(result[result.Length - 1])) {
                result.Length -= 1;
            }

            if (!string.IsNullOrEmpty(originalExpression)) {
                if (DisplayOptions.trimDocumentationText && originalExpression.Length > DisplayOptions.maxDocumentationTextLength) {
                    originalExpression = originalExpression.Substring(0,
                        Math.Max(3, DisplayOptions.maxDocumentationTextLength) - 3) + _ellipsis;
                }
                if (multiline) {
                    result.Insert(expressionPosition, $"{originalExpression}:{Environment.NewLine}");
                } else if (result.Length > 0) {
                    result.Insert(expressionPosition, $"{originalExpression}: ");
                } else {
                    result.Append($"{originalExpression}: <unknown type>");
                }
            }

            return result.ToString().TrimEnd();
        }

        public abstract string GetModuleDocumentation(ModuleReference modRef);
        protected abstract string MakeModuleDocumentation(AnalysisValue value);
        protected abstract string MakeFunctionDocumentation(AnalysisValue value);
        protected abstract string MakeClassDocumentation(AnalysisValue value);

        protected string LimitLines(
            string str,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str) || !DisplayOptions.trimDocumentationText) {
                return str;
            }

            var lineCount = 0;
            var prettyPrinted = new StringBuilder();
            var wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < DisplayOptions.maxDocumentationLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = DisplayOptions.maxDocumentationLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / DisplayOptions.maxDocumentationLineLength) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= DisplayOptions.maxDocumentationLines) {
                prettyPrinted.AppendLine(_ellipsis);
            }
            return prettyPrinted.ToString().Trim();
        }

        protected virtual string SoftWrap(string s) => s;
    }
}
