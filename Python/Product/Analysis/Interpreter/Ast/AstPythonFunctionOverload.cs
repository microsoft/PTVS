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
using System.Linq;
using Microsoft.PythonTools.Analysis;

namespace Microsoft.PythonTools.Interpreter.Ast {
    class AstPythonFunctionOverload : IPythonFunctionOverload, ILocatedMember {
        private readonly IReadOnlyList<IParameterInfo> _parameters;

        public AstPythonFunctionOverload(
            IEnumerable<IParameterInfo> parameters,
            LocationInfo loc
        ) {
            _parameters = parameters?.ToArray() ?? throw new ArgumentNullException(nameof(parameters));
            Locations = loc != null ? new[] { loc } : Array.Empty<LocationInfo>();
        }

        internal void SetDocumentation(string doc) {
            if (Documentation != null) {
                throw new InvalidOperationException("cannot set Documentation twice");
            }
            Documentation = doc;
        }

        internal List<IPythonType> ReturnTypes { get; } = new List<IPythonType>();

        public string Documentation { get; private set; }
        public string ReturnDocumentation { get; }
        public IParameterInfo[] GetParameters() => _parameters.ToArray();
        public IList<IPythonType> ReturnType => ReturnTypes.Where(v => v.TypeId != BuiltinTypeId.Unknown).ToArray();
        public IEnumerable<LocationInfo> Locations { get; }
    }
}
