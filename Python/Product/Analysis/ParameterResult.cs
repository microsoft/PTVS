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

namespace Microsoft.PythonTools.Analysis {
    public class ParameterResult : IEquatable<ParameterResult> {
        public string Name { get; private set; }
        public string Documentation { get; private set; }
        public string Type { get; private set; }
        public string DefaultValue { get; private set; }
        public bool IsOptional { get; private set; }
        public IEnumerable<IAnalysisVariable> Variables { get; private set; }

        public ParameterResult(string name)
            : this(name, String.Empty, "object") {
        }
        public ParameterResult(string name, string doc)
            : this(name, doc, "object") {
        }
        public ParameterResult(string name, string doc, string type)
            : this(name, doc, type, false) {
        }
        public ParameterResult(string name, string doc, string type, bool isOptional)
            : this(name, doc, type, isOptional, null) {
        }
        public ParameterResult(string name, string doc, string type, bool isOptional, IEnumerable<IAnalysisVariable> variable) :
            this(name, doc, type, isOptional, variable, null) {
        }
        public ParameterResult(string name, string doc, string type, bool isOptional, IEnumerable<IAnalysisVariable> variable, string defaultValue) {
            Name = name;
            Documentation = doc;
            Type = type;
            IsOptional = isOptional;
            Variables = variable;
            DefaultValue = defaultValue;
        }

        public override bool Equals(object obj) {
            return Equals(obj as ParameterResult);
        }

        public override int GetHashCode() {
            return Name.GetHashCode() ^
                (Type ?? "").GetHashCode() ^
                IsOptional.GetHashCode() ^
                (DefaultValue ?? "").GetHashCode();
        }

        public bool Equals(ParameterResult other) {
            return other != null &&
                Name == other.Name &&
                Documentation == other.Documentation &&
                Type == other.Type &&
                IsOptional == other.IsOptional &&
                DefaultValue == other.DefaultValue;
        }
    }
}
