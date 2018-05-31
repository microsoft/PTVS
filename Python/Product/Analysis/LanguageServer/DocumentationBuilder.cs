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

        protected DocumentationBuilder(InformationDisplayOptions displayOptions) {
            DisplayOptions = displayOptions;
        }

        public string GetDocumentation(IEnumerable<AnalysisValue> values, string originalExpression) {
            var array = values.ToArray();
            if (array.Length == 1) {
                var v = array[0];
                switch (v.MemberType) {
                    case PythonMemberType.Function:
                        return MakeFunctionDocumentation(v);
                    case PythonMemberType.Class:
                        return MakeClassDocumentation(v);
                    case PythonMemberType.Module:
                        return MakeModuleDocumentation(v);
                    case PythonMemberType.Constant:
                        return MakeConstantDocumentation(v);
                }
            }
            return MakeGeneralDocumentation(array, originalExpression);
        }

        private string MakeGeneralDocumentation(AnalysisValue[] values, string originalExpression) {
            var descriptions = new Dictionary<string, string>();
            var haveDocs = false;
            var multiline = false;
            var descPrefix = string.Empty;

            foreach (var v in values) {
                var d = v.Description;
                string doc = null;
                if (!string.IsNullOrEmpty(d)) {
                    if (!IsBasicType(v.PythonType)) {
                        doc = v.Documentation;
                        if (DisplayOptions.trimDocumentationLines) {
                            doc = LimitLines(doc);
                        }
                        haveDocs |= !string.IsNullOrEmpty(doc);
                    }
                    multiline |= d.IndexOf('\n') >= 0;
                    descriptions[d] = doc ?? string.Empty;
                }
            }

            if (descriptions.Count == 0) {
                return string.Empty;
            }

            var result = new StringBuilder();
            if (!haveDocs && !multiline) {
                // No documentation, simply description, just concatenate
                result.Append(string.Join(", ", descriptions.Keys));
            } else {
                descPrefix = DisplayOptions.preferredFormat == MarkupKind.Markdown ? $"```python{Environment.NewLine}" : string.Empty;
                var descSuffix = DisplayOptions.preferredFormat == MarkupKind.Markdown ? $"```{Environment.NewLine}" : string.Empty;
                multiline = descriptions.Count > 1;

                foreach (var kvp in descriptions) {
                    var desc = kvp.Key;
                    var doc = kvp.Value;

                    if (result.Length > 0) {
                        result.AppendLine();
                        result.AppendLine();
                    }
                    result.Append(descPrefix);
                    result.AppendLine(desc);
                    result.Append(descSuffix);

                    if (!string.IsNullOrEmpty(doc)) {
                        result.AppendLine(SoftWrap(doc));
                        multiline |= doc.IndexOf('\n') >= 0;
                    }

                    if (DisplayOptions.trimDocumentationText && result.Length > DisplayOptions.maxDocumentationTextLength) {
                        result.Length = Math.Max(0, DisplayOptions.maxDocumentationTextLength - 3);
                        result.Append(_ellipsis);
                        break;
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

                    result.TrimEnd();
                }
            }

            if (!string.IsNullOrEmpty(originalExpression)) {
                if (DisplayOptions.trimDocumentationText && originalExpression.Length > DisplayOptions.maxDocumentationTextLength) {
                    originalExpression = originalExpression.Substring(0,
                        Math.Max(3, DisplayOptions.maxDocumentationTextLength) - 3) + _ellipsis;
                }
                if (multiline || descPrefix.Length > 0) {
                    result.Insert(0, $"{originalExpression}:{Environment.NewLine}");
                } else if (result.Length > 0) {
                    result.Insert(0, $"{originalExpression}: ");
                } else {
                    result.Append($"{originalExpression}: <unknown type>");
                }
            }

            return result.TrimEnd().ToString();
        }

        public abstract string GetModuleDocumentation(ModuleReference modRef);
        protected abstract string MakeModuleDocumentation(AnalysisValue value);
        protected abstract string MakeFunctionDocumentation(AnalysisValue value);
        protected abstract string MakeClassDocumentation(AnalysisValue value);
        protected abstract string MakeConstantDocumentation(AnalysisValue value);

        protected string LimitLines(
            string str,
            bool ellipsisAtEnd = true,
            bool stopAtFirstBlankLine = false
        ) {
            if (string.IsNullOrEmpty(str)) {
                return string.Empty;
            }

            if(!DisplayOptions.trimDocumentationText) {
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

        private static bool IsBasicType(IPythonType type) {
            if (type == null || !type.IsBuiltin) {
                return false;
            }

            switch(type.TypeId) {
                case BuiltinTypeId.Bool:
                case BuiltinTypeId.Bytes:
                case BuiltinTypeId.Complex:
                case BuiltinTypeId.Dict:
                case BuiltinTypeId.Float:
                case BuiltinTypeId.Int:
                case BuiltinTypeId.Str:
                case BuiltinTypeId.Unicode:
                    return true;
            }
            return false;
        }
    }
}
