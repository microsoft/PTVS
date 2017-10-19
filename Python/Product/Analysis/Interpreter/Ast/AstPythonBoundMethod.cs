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

using System.Collections.Generic;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonBoundMethod : IPythonBoundFunction, ILocatedMember {
        public AstPythonBoundMethod(IPythonFunction function, IPythonType selfType) {
            Function = function;
            SelfType = selfType;
        }

        public IPythonFunction Function { get; }
        public IPythonType SelfType { get; }
        public PythonMemberType MemberType => PythonMemberType.Method;
        public IEnumerable<LocationInfo> Locations => (Function as ILocatedMember)?.Locations;
    }
}
