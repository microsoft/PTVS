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
using Microsoft.PythonTools.Analysis.Values;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis {
    sealed class ResolutionContext {
        private ArgumentSet? _callArgs;

        public static readonly ResolutionContext Empty = new ResolutionContext();
        public static readonly ResolutionContext Complete = new ResolutionContext { ResolveParametersFully = true };

        public FunctionInfo Caller { get; set; }
        public ArgumentSet CallArgs {
            get => _callArgs ?? LazyCallArgs?.Value ?? default(ArgumentSet);
            set => _callArgs = value;
        }
        public Lazy<ArgumentSet> LazyCallArgs { get; set; }
        public Node CallSite { get; set; }
        public bool ResolveParametersFully { get; set; }
    }
}
