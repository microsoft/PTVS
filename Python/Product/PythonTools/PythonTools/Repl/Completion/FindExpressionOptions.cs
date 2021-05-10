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
// MERCHANTABILITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

namespace Microsoft.PythonTools.Repl.Completion {
    public struct FindExpressionOptions {
        public static FindExpressionOptions Hover => new FindExpressionOptions {
            Calls = true,
            Indexing = true,
            Names = true,
            Members = true,
            ParameterNames = true,
            ParenthesisedExpression = true,
            ImportNames = true,
            ImportAsNames = true,
            ClassDefinitionName = true,
            FunctionDefinitionName = true,
        };
        public static FindExpressionOptions FindDefinition => new FindExpressionOptions {
            Names = true,
            Members = true,
            ParameterNames = true,
            NamedArgumentNames = true,
            ImportNames = true,
            ImportAsNames = true,
            ClassDefinitionName = true,
            FunctionDefinitionName = true,
        };
        public static FindExpressionOptions Rename => new FindExpressionOptions {
            Names = true,
            MemberName = true,
            NamedArgumentNames = true,
            ParameterNames = true,
            ImportNames = true,
            ImportAsNames = true,
            ClassDefinitionName = true,
            FunctionDefinitionName = true,
        };
        public static FindExpressionOptions Complete => new FindExpressionOptions {
            Names = true,
            Members = true,
            NamedArgumentNames = true,
            ImportNames = true,
            ImportAsNames = true,
            Literals = true,
            Errors = true
        };

        public bool Calls { get; set; }
        public bool Indexing { get; set; }
        public bool Names { get; set; }
        public bool Members { get; set; }
        public bool MemberTarget { get; set; }
        public bool MemberName { get; set; }
        public bool Literals { get; set; }
        public bool Keywords { get; set; }
        public bool ParenthesisedExpression { get; set; }
        public bool NamedArgumentNames { get; set; }
        public bool ParameterNames { get; set; }
        public bool ClassDefinition { get; set; }
        public bool ClassDefinitionName { get; set; }
        public bool FunctionDefinition { get; set; }
        public bool FunctionDefinitionName { get; set; }
        public bool ImportNames { get; set; }
        public bool ImportAsNames { get; set; }
        public bool Errors { get; set; }
    }
}