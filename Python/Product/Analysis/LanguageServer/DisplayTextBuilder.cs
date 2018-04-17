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
using System.Text;
using Microsoft.PythonTools.Analysis.Infrastructure;
using Microsoft.PythonTools.Interpreter;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    sealed class DisplayTextBuilder {
        private readonly RestTextConverter _textConverter = new RestTextConverter();

        public string MakeHoverText(IEnumerable<AnalysisValue> values, string originalExpression, InformationDisplayOptions displayOptions) {
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

                if ((d?.Length ?? 0) < (doc?.Length ?? 0)) {
                    if (displayOptions.trimDocumentationLines) {
                        doc = LimitLines(doc);
                    }
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

            var displayText = result.ToString().TrimEnd();
            var multiline = displayText.IndexOf('\n') >= 0;
            if (displayOptions.trimDocumentationText && displayText.Length > displayOptions.maxDocumentationTextLength) {
                displayText = displayText.Substring(0,
                    Math.Max(3, displayOptions.maxDocumentationTextLength) - 3) + "...";

                result.Clear();
                result.Append(displayText);
            }

            if (!string.IsNullOrEmpty(originalExpression)) {
                if (displayOptions.trimDocumentationText && originalExpression.Length > displayOptions.maxDocumentationTextLength) {
                    originalExpression = originalExpression.Substring(0, 
                        Math.Max(3, displayOptions.maxDocumentationTextLength) - 3) + "...";
                }
                if (multiline) {
                    result.Insert(0, $"{originalExpression}:{Environment.NewLine}");
                } else if (result.Length > 0) {
                    result.Insert(0, $"{originalExpression}: ");
                } else {
                    result.Append($"{originalExpression}: <unknown type>");
                }
            }

            return _textConverter.ToMarkdown(result.ToString());
        }

        public string MakeModuleHoverText(ModuleReference modRef) {
            // Return module information
            var contents = "{0} : module".FormatUI(modRef.Name);
            if (!string.IsNullOrEmpty(modRef.Module?.Documentation)) {
                contents += $"{Environment.NewLine}{Environment.NewLine}{modRef.Module.Documentation}";
            }
            return contents;
        }

        private static string LimitLines(
            string str,
            int maxLines = 30,
            int charsPerLine = 200,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str)) {
                return str;
            }

            var lineCount = 0;
            var prettyPrinted = new StringBuilder();
            var wasEmpty = true;

            using (var reader = new StringReader(str)) {
                for (var line = reader.ReadLine(); line != null && lineCount < maxLines; line = reader.ReadLine()) {
                    if (string.IsNullOrWhiteSpace(line)) {
                        if (wasEmpty) {
                            continue;
                        }
                        wasEmpty = true;
                        if (stopAtFirstBlankLine) {
                            lineCount = maxLines;
                            break;
                        }
                        lineCount += 1;
                        prettyPrinted.AppendLine();
                    } else {
                        wasEmpty = false;
                        lineCount += (line.Length / charsPerLine) + 1;
                        prettyPrinted.AppendLine(line);
                    }
                }
            }
            if (ellipsisAtEnd && lineCount >= maxLines) {
                prettyPrinted.AppendLine("...");
            }
            return prettyPrinted.ToString().Trim();
        }
    }
}
