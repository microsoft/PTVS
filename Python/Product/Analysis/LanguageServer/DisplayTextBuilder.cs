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

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    sealed class DisplayTextBuilder {
        private readonly RestTextConverter _textConverter = new RestTextConverter();
        public void BuildMarkdownSignature(SignatureHelp signatureHelp) {
            foreach (var s in signatureHelp.signatures) {
                // Recostruct full signature so editor can display current parameter
                var sb = new StringBuilder();

                if (s.documentation != null) {
                    s.documentation.value = _textConverter.ToMarkdown(s.documentation.value);
                }
                sb.Append(s.label);
                sb.Append('(');
                if (s.parameters != null) {
                    foreach (var p in s.parameters) {
                        if (sb[sb.Length - 1] != '(') {
                            sb.Append(", ");
                        }
                        sb.Append(p.label);
                        if (p.documentation != null) {
                            p.documentation.value = _textConverter.ToMarkdown(p.documentation.value);
                        }
                    }
                }
                sb.Append(')');
                s.label = sb.ToString();
            }
        }

        public string MakeHoverText(IEnumerable<AnalysisValue> values, string originalExpression, InformationDisplayOptions displayOptions) {
            string firstLongDescription = null;
            var result = new StringBuilder();
            var documentations = new HashSet<string>();

            foreach (var v in values) {
                var doc = !string.IsNullOrEmpty(v.Documentation) ? v.Documentation : string.Empty;
                var desc = !string.IsNullOrEmpty(v.Description) ? v.Description : string.Empty;
                doc = doc.Length > desc.Length ? doc : desc;
                firstLongDescription = firstLongDescription ?? doc;

                doc = displayOptions.trimDocumentationLines ? LimitLines(doc) : doc;
                if (string.IsNullOrEmpty(doc)) {
                    continue;
                }

                if (documentations.Add(doc)) {
                    if (documentations.Count > 1) {
                        if (result.Length == 0) {
                            // Nop
                        } else if (result[result.Length - 1] != '\n') {
                            result.Append(", ");
                        }
                    }
                    result.Append(doc);
                }
            }

            if (documentations.Count == 1 && !string.IsNullOrEmpty(firstLongDescription)) {
                result.Clear();
                result.Append(firstLongDescription);
            }

            var displayText = result.ToString();
            if (displayOptions.trimDocumentationText && displayText.Length > displayOptions.maxDocumentationTextLength) {
                displayText = displayText.Substring(0,
                    Math.Max(3, displayOptions.maxDocumentationTextLength) - 3) + "...";

                result.Clear();
                result.Append(displayText);
            }

            if (!string.IsNullOrEmpty(originalExpression)) {
                if (result.Length == 0) {
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
