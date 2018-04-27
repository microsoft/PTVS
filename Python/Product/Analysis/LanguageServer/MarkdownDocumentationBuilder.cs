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

using System.Text;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class MarkdownDocumentationBuilder : DocumentationBuilder {
        public MarkdownDocumentationBuilder(InformationDisplayOptions displayOptions) : base(displayOptions) { }

        public override string GetModuleDocumentation(ModuleReference modRef) {
            var doc = modRef.Module?.Documentation;
            return doc != null ? new RestTextConverter().ToMarkdown(LimitLines(doc)) : $"module {modRef.Name}";
        }

        protected override string MakeFunctionDocumentation(AnalysisValue value) {
            //var subHeader = value.PythonType?.IsBuiltin == true ? "[built-in function]" : string.Empty;
            return FromDocAndDescription(value, string.Empty);
        }
        protected override string MakeModuleDocumentation(AnalysisValue value) {
            //var subHeader = value.PythonType?.IsBuiltin == true ? "[built-in module]" : string.Empty;
            return FromDocAndDescription(value, string.Empty);
        }
        protected override string MakeClassDocumentation(AnalysisValue value) {
            //var subHeader = value.PythonType?.IsBuiltin == true ? "[built-in class]" : string.Empty;
            return FromDocAndDescription(value, string.Empty);
        }

        private string FromDocAndDescription(AnalysisValue value, string subHeader) {
            var sb = new StringBuilder();
            sb.AppendLine("```python");
            sb.AppendLine(value.Description);
            sb.AppendLine("```");
            if(!string.IsNullOrEmpty(subHeader)) {
                sb.AppendLine(subHeader);
            }
            var doc = LimitLines(value.Documentation).Trim();
            doc = new RestTextConverter().ToMarkdown(doc);
            sb.AppendLine();
            sb.AppendLine();
            sb.Append(doc);
            return sb.ToString();
        }
    }
}
