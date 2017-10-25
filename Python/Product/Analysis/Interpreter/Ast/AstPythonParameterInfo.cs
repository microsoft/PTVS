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
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonParameterInfo : IParameterInfo {
        public AstPythonParameterInfo(PythonAst ast, Parameter p) {
            Name = p?.Name ?? throw new ArgumentNullException(nameof(p));
            Documentation = "";
            DefaultValue = p.DefaultValue?.ToCodeString(ast).Trim();
            IsParamArray = p.Kind == ParameterKind.List;
            IsKeywordDict = p.Kind == ParameterKind.Dictionary;
            ParameterTypes = new IPythonType[0];
        }

        public string Name { get; }
        public string Documentation { get; }
        public string DefaultValue { get; }
        public bool IsParamArray { get; }
        public bool IsKeywordDict { get; }
        public IList<IPythonType> ParameterTypes { get; }
    }
}
