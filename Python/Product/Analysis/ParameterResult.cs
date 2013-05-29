/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

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
            Documentation = Trim(doc);
            Type = type;
            IsOptional = isOptional;
            Variables = variable;
            DefaultValue = defaultValue;
        }

        private const int MaxDocLength = 1000;
        internal static string Trim(string doc) {
            if (doc != null && doc.Length > MaxDocLength) {
                return doc.Substring(0, MaxDocLength) + "...";
            }
            return doc;
        }

        public override bool Equals(object obj) {
            return Equals(obj as ParameterResult);
        }

        public override int GetHashCode() {
            return Name.GetHashCode() ^
                Type.GetHashCode() ^
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
