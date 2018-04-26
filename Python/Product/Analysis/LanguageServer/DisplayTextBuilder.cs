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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    sealed class DisplayTextBuilder {
        public string MakeHoverText(IEnumerable<AnalysisValue> values, string originalExpression, InformationDisplayOptions displayOptions) {
            if (values.Count() == 1) {
                var v = values.First();
                switch (v.MemberType) {
                    case PythonMemberType.Function:
                        return MakeFunctionHoverText(values.First(), displayOptions);
                    case PythonMemberType.Class:
                        return MakeClassHoverText(values.First(), displayOptions);
                }
            }
            return MakeOtherHoverText(values, originalExpression, displayOptions);
        }

        private static string MakeOtherHoverText(IEnumerable<AnalysisValue> values, string originalExpression, InformationDisplayOptions displayOptions) {
            var result = new StringBuilder();
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
                if (displayOptions.trimDocumentationLines) {
                    doc = LimitLines(doc, displayOptions);
                }

                if ((d?.Length ?? 0) < (doc?.Length ?? 0)) {
                    if (!string.IsNullOrEmpty(doc)) {
                        documentations.Add(doc);
                    }
                    continue;
                }

                if (!string.IsNullOrEmpty(d)) {
                    descriptions.Add(d);
                }
            }

            foreach (var d in descriptions.Ordered()) {
                if (result.Length > 0) {
                    result.Append(", ");
                }
                result.Append(d);
            }

            foreach (var d in documentations.Ordered()) {
                if (result.Length > 0) {
                    result.AppendLine();
                }

                result.AppendLine(d);
            }

            if (displayOptions.trimDocumentationText && result.Length > displayOptions.maxDocumentationTextLength) {
                result.Length = Math.Max(0, displayOptions.maxDocumentationTextLength - 3);
                result.Append("...");
            } else if (displayOptions.trimDocumentationLines) {
                using (var sr = new StringReader(result.ToString())) {
                    result.Clear();
                    int lines = displayOptions.maxDocumentationLineLength;
                    for (var line = sr.ReadLine(); line != null; line = sr.ReadLine()) {
                        if (--lines < 0) {
                            result.Append("...");
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
                if (displayOptions.trimDocumentationText && originalExpression.Length > displayOptions.maxDocumentationTextLength) {
                    originalExpression = originalExpression.Substring(0,
                        Math.Max(3, displayOptions.maxDocumentationTextLength) - 3) + "...";
                }
                if (result.ToString().IndexOf('\n') >= 0) {
                    result.Insert(0, $"{originalExpression}:{Environment.NewLine}");
                } else if (result.Length > 0) {
                    result.Insert(0, $"{originalExpression}: ");
                } else {
                    result.Append($"{originalExpression}: <unknown type>");
                }
            }

            Debug.Write(result.ToString());
            return result.ToString().TrimEnd();
        }

        public string MakeModuleHoverText(ModuleReference modRef, InformationDisplayOptions displayOptions) {
            // Return module information
            var contents = $"module {modRef.Name}";
            if (!string.IsNullOrEmpty(modRef.Module?.Documentation)) {
                var doc = LimitLines(modRef.Module.Documentation, displayOptions);
                contents += $"{Environment.NewLine}{Environment.NewLine}{doc}";
            }
            return contents;
        }

        private string MakeFunctionHoverText(AnalysisValue value, InformationDisplayOptions displayOptions) {
            if (displayOptions.preferredFormat == MarkupKind.Markdown) {
                var doc = value.Documentation ?? value.Description;
                var paraIndex = doc.IndexOf("\n\n");
                paraIndex = paraIndex > 0 ? paraIndex : doc.IndexOf("\r\n\r\n");
                if (paraIndex > 0) {
                    var md = $"```python\n{doc.Substring(0, paraIndex)}\n```";
                    md += $"{Environment.NewLine}{Environment.NewLine}{doc.Substring(paraIndex).Trim()}";
                    return md;
                }
            }

            var contents = value.Description;
            if (!string.IsNullOrEmpty(value.Documentation)) {
                var doc = LimitLines(value.Documentation, displayOptions);
                contents += $"{Environment.NewLine}{Environment.NewLine}{doc}";
            }
            return contents;
        }

        private string MakeClassHoverText(AnalysisValue value, InformationDisplayOptions displayOptions) {
            if (displayOptions.preferredFormat == MarkupKind.Markdown) {
                var md = $"```python\n{value.Description}\n```";
                if (!string.IsNullOrEmpty(value.Documentation)) {
                    md += $"{Environment.NewLine}{Environment.NewLine}{value.Documentation}";
                }
                return md;
            }

            var contents = value.Description;
            if (!string.IsNullOrEmpty(value.Documentation)) {
                var doc = LimitLines(value.Documentation, displayOptions);
                contents += $"{Environment.NewLine}{Environment.NewLine}{doc}";
            }
            return contents;
        }

        private static string LimitLines(
            string str,
            InformationDisplayOptions displayOptions,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str) || !displayOptions.trimDocumentationText) {
                return str;
            }

            var lineCount = 0;
            var prettyPrinted = new StringBuilder();
            var wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < displayOptions.maxDocumentationLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = displayOptions.maxDocumentationLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / displayOptions.maxDocumentationLineLength) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= displayOptions.maxDocumentationLines) {
                prettyPrinted.AppendLine("...");
            }
            return prettyPrinted.ToString().Trim();
        }
    }
}
