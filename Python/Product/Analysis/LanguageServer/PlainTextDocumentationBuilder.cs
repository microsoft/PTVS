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
using System.Text;

namespace Microsoft.PythonTools.Analysis.LanguageServer {
    internal sealed class PlainTextDocumentationBuilder : DocumentationBuilder {
        public PlainTextDocumentationBuilder(InformationDisplayOptions displayOptions) : base(displayOptions) { }

        public override string GetModuleDocumentation(ModuleReference modRef) {
            var prefix = modRef.AnalysisModule?.PythonType?.IsBuiltin == true ? "built-in module " : "module ";
            var contents = $"{prefix}{modRef.Name}";
            var doc = modRef.Module?.Documentation;
            if (!string.IsNullOrEmpty(doc)) {
                doc = LimitLines(modRef.Module.Documentation);
                contents += $"{Environment.NewLine}{Environment.NewLine}{doc}";
            }
            return contents;
        }

        protected override string MakeModuleDocumentation(AnalysisValue value) {
            var prefix = value.PythonType?.IsBuiltin == true ? "built-in module " : "module ";
            return FromDocAndDescription(value, prefix);
        }
        protected override string MakeFunctionDocumentation(AnalysisValue value) {
            var prefix = value.PythonType?.IsBuiltin == true ? "built-in function " : "function ";
            return FromDocAndDescription(value, prefix);
        }
        protected override string MakeClassDocumentation(AnalysisValue value) => FromDocAndDescription(value, string.Empty);
        protected override string MakeConstantDocumentation(AnalysisValue value) => value.Description;

        private string FromDocAndDescription(AnalysisValue value, string prefix) {
            var sb = new StringBuilder();
            if(!string.IsNullOrEmpty(prefix)) {
                sb.Append(prefix);
            }
            sb.Append(value.Description);
            if (!string.IsNullOrEmpty(value.Documentation)) {
                var doc = LimitLines(value.Documentation);
                sb.AppendLine();
                sb.AppendLine();
                sb.Append(doc);
            }
            return sb.ToString();
        }
    }
}
